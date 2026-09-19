"""
Generateur des bruitages du jeu.

Les sons ne sont pas des enregistrements : ils sont synthetises ici, puis ecrits en WAV mono
16 bits 44100 Hz dans SettlersOfIdlestanSkia/Resources/sounds/, d'ou ils sont embarques comme
ressources (voir SettlersOfIdlestanSkia.csproj). Un fichier genere ici porte le meme nom que la
valeur de SoundId qui le joue, en snake_case : c'est ce qui les relie (voir SoundBank.cs).

Relancer apres toute retouche :

    python assets/sounds/generate_sounds.py

Remplacer un son par un vrai enregistrement ne demande aucun changement de code : il suffit de
deposer un WAV du meme nom dans Resources/sounds/ (mono ou stereo, 16 bits ou 32 bits flottants,
n'importe quelle frequence : SoundBank reechantillonne).

Deux regles valent pour tous les sons :
  - duree courte (< 1 s) : ce sont des retours d'interface, pas de la musique ;
  - fondu de fin systematique (voir `finish`), sinon la coupure nette claque dans le haut-parleur.
"""

import math
import os
import random
import struct
import wave

SAMPLE_RATE = 44100
OUT_DIR = os.path.join(os.path.dirname(os.path.abspath(__file__)),
                       "..", "..", "SettlersOfIdlestanSkia", "Resources", "sounds")

# Graine fixe : le bruit (attaques, destructions) doit etre identique d'une generation a l'autre,
# sinon chaque relance du script reecrit tous les fichiers et pollue le diff.
random.seed(20260919)


# -- Briques de synthese -----------------------------------------------------

def silence(duration):
    return [0.0] * int(duration * SAMPLE_RATE)


def _wave_sample(kind, phase):
    """phase en tours (0..1), pas en radians."""
    p = phase % 1.0
    if kind == "sine":
        return math.sin(2 * math.pi * p)
    if kind == "triangle":
        return 4 * abs(p - 0.5) - 1
    if kind == "square":
        return 1.0 if p < 0.5 else -1.0
    if kind == "saw":
        return 2 * p - 1
    raise ValueError(kind)


def tone(freq, duration, kind="sine", freq_end=None, vibrato=0.0, vibrato_hz=6.0):
    """
    Oscillateur a frequence glissante. `freq_end` fait glisser la hauteur en exponentielle : une
    glissade lineaire en Hz s'entend comme une courbe, l'oreille percevant les octaves.
    """
    n = int(duration * SAMPLE_RATE)
    out = [0.0] * n
    phase = 0.0
    end = freq if freq_end is None else freq_end
    for i in range(n):
        t = i / n if n else 0.0
        f = freq * (end / freq) ** t
        if vibrato:
            f *= 1.0 + vibrato * math.sin(2 * math.pi * vibrato_hz * i / SAMPLE_RATE)
        phase += f / SAMPLE_RATE
        out[i] = _wave_sample(kind, phase)
    return out


def noise(duration):
    n = int(duration * SAMPLE_RATE)
    return [random.uniform(-1.0, 1.0) for _ in range(n)]


def envelope(buf, attack=0.005, curve=3.0, sustain=0.0):
    """
    Enveloppe percussive : montee lineaire courte puis descente en puissance. `curve` > 1 raccourcit
    la queue (plus sec), < 1 l'allonge (plus resonant). `sustain` maintient un plateau avant la
    descente, pour les sons tenus (fanfare).
    """
    n = len(buf)
    if n == 0:
        return buf
    a = max(1, int(attack * SAMPLE_RATE))
    s = int(sustain * SAMPLE_RATE)
    for i in range(n):
        if i < a:
            g = i / a
        elif i < a + s:
            g = 1.0
        else:
            x = (i - a - s) / max(1, n - a - s)
            g = (1.0 - x) ** curve
        buf[i] *= g
    return buf


def lowpass(buf, cutoff):
    """Un pole, suffisant pour arrondir un carre ou un bruit blanc."""
    if not buf:
        return buf
    dt = 1.0 / SAMPLE_RATE
    rc = 1.0 / (2 * math.pi * cutoff)
    a = dt / (rc + dt)
    out = [0.0] * len(buf)
    prev = 0.0
    for i, v in enumerate(buf):
        prev += a * (v - prev)
        out[i] = prev
    return out


def highpass(buf, cutoff):
    if not buf:
        return buf
    dt = 1.0 / SAMPLE_RATE
    rc = 1.0 / (2 * math.pi * cutoff)
    a = rc / (rc + dt)
    out = [0.0] * len(buf)
    prev_in = 0.0
    prev_out = 0.0
    for i, v in enumerate(buf):
        prev_out = a * (prev_out + v - prev_in)
        prev_in = v
        out[i] = prev_out
    return out


def gain(buf, g):
    return [v * g for v in buf]


def mix(*buffers):
    n = max((len(b) for b in buffers), default=0)
    out = [0.0] * n
    for b in buffers:
        for i, v in enumerate(b):
            out[i] += v
    return out


def at(buf, delay):
    """Decale un buffer dans le temps."""
    return silence(delay) + buf


def bell(freq, duration, brightness=0.35, curve=3.0):
    """
    Cloche : fondamentale sinus plus une douzieme (x3) discrete. C'est ce timbre qui donne aux
    toasts leur caractere "interface" sans sonner synthetique.
    """
    base = envelope(tone(freq, duration, "sine"), attack=0.004, curve=curve)
    harm = envelope(tone(freq * 3, duration * 0.6, "sine"), attack=0.002, curve=curve + 2)
    return mix(base, gain(harm, brightness))


def finish(buf, peak=0.72, fade=0.02):
    """Normalise au pic voulu et impose un fondu de fin (voir l'entete)."""
    m = max((abs(v) for v in buf), default=0.0)
    if m > 0:
        buf = gain(buf, peak / m)
    f = min(len(buf), int(fade * SAMPLE_RATE))
    for i in range(f):
        buf[len(buf) - f + i] *= 1.0 - i / f
    return buf


def write(name, buf):
    path = os.path.normpath(os.path.join(OUT_DIR, name + ".wav"))
    frames = bytearray()
    for v in buf:
        s = int(max(-1.0, min(1.0, v)) * 32767)
        frames += struct.pack("<h", s)
    with wave.open(path, "wb") as f:
        f.setnchannels(1)
        f.setsampwidth(2)
        f.setframerate(SAMPLE_RATE)
        f.writeframes(bytes(frames))
    print("%-22s %5.2f s  %4d Ko" % (name + ".wav", len(buf) / SAMPLE_RATE, len(frames) // 1024))


# -- Les sons ----------------------------------------------------------------

def toast_info():
    """Deux cloches douces, seconde majeure ascendante. Neutre, ne doit rien evoquer."""
    return mix(bell(784, 0.30),
               at(gain(bell(880, 0.34), 0.85), 0.09))


def toast_warning():
    """
    Corne basse a deux notes descendantes, carre adouci. Une menace vient d'apparaitre : le son
    doit attirer l'oeil sans faire sursauter, d'ou le filtre a 900 Hz qui retire le mordant.
    """
    a = envelope(tone(311, 0.22, "square"), attack=0.012, sustain=0.06, curve=2.0)
    b = envelope(tone(233, 0.34, "square"), attack=0.012, sustain=0.08, curve=1.8)
    return lowpass(mix(a, at(b, 0.17)), 900)


def toast_victory():
    """Arpege majeur ascendant : un monstre est tombe, une civilisation s'est eteinte."""
    notes = [(523, 0.00), (659, 0.07), (784, 0.14), (1047, 0.21)]
    return mix(*[at(gain(bell(f, 0.34 + i * 0.06), 0.9 ** i), d)
                 for i, (f, d) in enumerate(notes)])


def toast_loss():
    """
    Chute mineure a trois notes, legerement desaccordee. Reservee aux vraies pertes (ville rasee,
    portail perdu) : c'est le seul son du jeu qui s'autorise a sonner faux.
    """
    notes = [(440, 0.00), (349, 0.13), (262, 0.26)]
    voices = []
    for f, d in notes:
        voices.append(at(bell(f, 0.42, brightness=0.15, curve=2.2), d))
        voices.append(at(gain(bell(f * 0.995, 0.42, brightness=0.1, curve=2.2), 0.6), d))
    return lowpass(mix(*voices), 2600)


def achievement():
    """
    Fanfare : meme famille que la victoire, une octave plus haut et doublee a l'octave superieure
    pour le scintillement. Doit rester reconnaissable entre mille : c'est la recompense.
    """
    notes = [(784, 0.00), (1047, 0.08), (1319, 0.16), (1568, 0.24)]
    voices = []
    for i, (f, d) in enumerate(notes):
        voices.append(at(bell(f, 0.40 + i * 0.08, brightness=0.45), d))
        voices.append(at(gain(bell(f * 2, 0.26, brightness=0.2), 0.22), d + 0.015))
    return mix(*voices)


def harvest_manual():
    """
    Le son le plus joue du jeu : le joueur clique sur une tuile toutes les deux secondes pendant
    des heures. Tout ce qui pourrait fatiguer est retire : pas d'harmonique aigue, 90 ms, et un
    transitoire de bruit tres court qui donne le "toc" sans timbre identifiable.
    """
    pluck = envelope(tone(620, 0.09, "triangle", freq_end=560), attack=0.002, curve=4.0)
    click = envelope(lowpass(noise(0.012), 3000), attack=0.001, curve=5.0)
    return finish(mix(pluck, gain(click, 0.35)), peak=0.45)


def attack_dealt():
    """Coup porte : bruit filtre haut, bref et clair. Joue a chaque salve, donc tres court."""
    swish = envelope(highpass(noise(0.10), 1800), attack=0.002, curve=4.5)
    blip = envelope(tone(900, 0.07, "triangle", freq_end=1500), attack=0.001, curve=4.0)
    return finish(mix(gain(swish, 0.8), gain(blip, 0.35)), peak=0.5)


def attack_taken():
    """
    Coup encaisse : meme cadence que l'attaque portee, mais grave et mat. L'oreille distingue
    immediatement qui frappe qui sans regarder la carte.
    """
    thud = envelope(tone(150, 0.17, "sine", freq_end=70), attack=0.002, curve=3.0)
    body = envelope(lowpass(noise(0.14), 700), attack=0.002, curve=3.5)
    return finish(mix(thud, gain(body, 0.5)), peak=0.55)


def building_destroyed():
    """Effondrement : meme grave que le coup encaisse, tenu plus longtemps, avec un gravier."""
    crash = envelope(lowpass(noise(0.40), 1100), attack=0.004, curve=2.2)
    boom = envelope(tone(110, 0.32, "sine", freq_end=48), attack=0.003, curve=2.6)
    return finish(mix(gain(crash, 0.7), boom), peak=0.6)


def building_built():
    """Pose de charpente : un "tock" de bois puis deux notes montantes, tres discretes."""
    knock = envelope(lowpass(noise(0.05), 1600), attack=0.001, curve=5.0)
    a = envelope(tone(523, 0.10, "triangle"), attack=0.003, curve=3.5)
    b = envelope(tone(698, 0.13, "triangle"), attack=0.003, curve=3.5)
    return finish(mix(gain(knock, 0.45), gain(a, 0.6), at(gain(b, 0.5), 0.07)), peak=0.45)


def city_founded():
    """Triade chaude qui s'ouvre : une ville de plus, moment rare et positif."""
    notes = [262, 330, 392]
    voices = [at(envelope(tone(f, 0.46, "triangle"), attack=0.05, sustain=0.08, curve=2.0), i * 0.04)
              for i, f in enumerate(notes)]
    shimmer = at(gain(bell(1047, 0.30, brightness=0.3), 0.3), 0.12)
    return lowpass(mix(*voices, shimmer), 4500)


SOUNDS = {
    "toast_info": toast_info,
    "toast_warning": toast_warning,
    "toast_victory": toast_victory,
    "toast_loss": toast_loss,
    "achievement": achievement,
    "harvest_manual": harvest_manual,
    "attack_dealt": attack_dealt,
    "attack_taken": attack_taken,
    "building_destroyed": building_destroyed,
    "building_built": building_built,
    "city_founded": city_founded,
}


def main():
    os.makedirs(os.path.normpath(OUT_DIR), exist_ok=True)
    for name, make in SOUNDS.items():
        buf = make()
        # Les sons les plus joues appellent deja `finish` avec un pic sur mesure, volontairement
        # plus bas ; les autres prennent le pic par defaut.
        peak = max((abs(v) for v in buf), default=0.0)
        write(name, finish(buf) if peak > 0.73 else finish(buf, peak=peak))


if __name__ == "__main__":
    main()
