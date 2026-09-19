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

C'est le cas de toast_warning, seul son importe du jeu : l'original est assets/sounds/
toast_warning.mp3 (Epidemic Sound, "User Interface, Alert, Attention, Sudden"), converti une
fois en WAV mono 44100 Hz et coupe a 0.95 s. Il n'est donc pas dans SOUNDS et ce script ne le
reecrit jamais. Les trois autres toasts sont bien synthetises ici, mais calques sur lui pour
que la famille tienne ensemble : voir `chime`.

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
    Cloche : fondamentale sinus plus une douzieme (x3) discrete. Timbre des sons d'evenement
    (succes, fondation, chute d'une ville) : franc a l'attaque, eteint en quelques centaines de
    millisecondes. Les toasts, eux, prennent la voix plus douce et plus longue de `chime`.
    """
    base = envelope(tone(freq, duration, "sine"), attack=0.004, curve=curve)
    harm = envelope(tone(freq * 3, duration * 0.6, "sine"), attack=0.002, curve=curve + 2)
    return mix(base, gain(harm, brightness))


def chime(freq, duration, swell=0.08, sustain=0.10, curve=1.5,
          octave=0.75, twelfth=0.50, fourth=0.26):
    """
    Voix des toasts, relevee sur toast_warning.wav (l'enregistrement importe, voir l'entete).
    Trois traits la distinguent de `bell`, et ce sont eux qui font la famille :

      - pas de transitoire : l'attaque est un fondu de 80 ms, le son s'ouvre au lieu de claquer ;
      - les quatre premiers partiels sont presque aussi forts que la fondamentale, ce qui donne
        le timbre de verre de l'original (la moitie de son energie est entre 800 et 2000 Hz) ;
      - la queue est longue et tenue, la ou les cloches du jeu s'eteignent en 300 ms.

    Rien au-dessus de 5 kHz : l'original n'y a rien non plus, et c'est ce qui lui permet d'etre
    entendu sans etre agressif.
    """
    base = envelope(tone(freq, duration, "sine"),
                    attack=swell, sustain=sustain, curve=curve)
    p2 = envelope(tone(freq * 2, duration * 0.85, "sine"),
                  attack=swell * 0.9, sustain=sustain * 0.7, curve=curve + 0.3)
    p3 = envelope(tone(freq * 3, duration * 0.75, "sine"),
                  attack=swell * 0.7, sustain=sustain * 0.5, curve=curve + 0.6)
    p4 = envelope(tone(freq * 4, duration * 0.55, "sine"),
                  attack=swell * 0.6, curve=curve + 1.0)
    return mix(base, gain(p2, octave), gain(p3, twelfth), gain(p4, fourth))


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

# Les trois toasts synthetises. toast_warning est importe (voir l'entete) : c'est lui qui fixe
# le timbre (`chime`), la duree (~1 s, queue comprise) et surtout le niveau. Il sort deux fois
# plus bas que les anciennes cloches, d'ou les pics sur mesure ci-dessous : cales a l'oreille
# electronique (RMS pondere A sur les 300 ms les plus fortes), les quatre toasts tombent a
# -20 dB a un demi-decibel pres, donc aucun ne saute au visage a cote des autres.
#
# Ils sont aussi tous en re, la ou l'importe est un septieme diminue de do diese, l'accord qui
# appelle re : la menace reste en suspens, les trois autres se posent.

def toast_info():
    """
    Quinte a vide re-la doublee a l'octave : ni majeur ni mineur, rien a evoquer. Le plus court
    et le plus discret des quatre, parce que c'est celui qui sonne le plus souvent.
    """
    return finish(lowpass(mix(chime(587, 0.72),
                              at(gain(chime(880, 0.62), 0.7), 0.07),
                              gain(chime(1175, 0.50), 0.26)), 6000), peak=0.30)


def toast_victory():
    """
    Re majeur qui monte, puis se repete une octave plus haut en echo lointain : un monstre est
    tombe, une civilisation s'est eteinte. L'echo est le geste de l'importe, qui se redit lui
    aussi a mi-parcours.
    """
    notes = [(587, 0.00), (740, 0.06), (880, 0.12), (1175, 0.18)]
    voices = [at(gain(chime(f, 0.95 - i * 0.10, sustain=0.12), 0.9 ** i), d)
              for i, (f, d) in enumerate(notes)]
    echo = at(gain(mix(chime(880, 0.42), gain(chime(1175, 0.38), 0.7)), 0.28), 0.46)
    return finish(lowpass(mix(*voices, echo), 6000), peak=0.40)


def toast_loss():
    """
    Re mineur qui retombe d'une tierce, chaque note doublee un demi-pour-cent plus bas. Reservee
    aux vraies pertes (ville rasee, portail perdu) : c'est le seul son du jeu qui s'autorise a
    sonner faux, le battement entre les deux voix en fait un accord qui se derobe.
    """
    def cluster(freqs, duration, level):
        voices = []
        for f in freqs:
            voices.append(gain(chime(f, duration), level))
            voices.append(gain(chime(f * 0.994, duration, octave=0.50, twelfth=0.26, fourth=0.10),
                               level * 0.5))
        return mix(*voices)

    return finish(lowpass(mix(cluster([587, 698, 880], 0.88, 1.0),
                              at(cluster([466, 587, 698], 0.56, 0.45), 0.34)), 5000), peak=0.36)


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


def city_founded():
    """Triade chaude qui s'ouvre : une ville de plus, moment rare et positif."""
    notes = [262, 330, 392]
    voices = [at(envelope(tone(f, 0.46, "triangle"), attack=0.05, sustain=0.08, curve=2.0), i * 0.04)
              for i, f in enumerate(notes)]
    shimmer = at(gain(bell(1047, 0.30, brightness=0.3), 0.3), 0.12)
    return lowpass(mix(*voices, shimmer), 4500)


def city_lost():
    """
    Une de nos villes vient de tomber. Glas grave a deux notes descendantes, sur un grondement
    d'effondrement qui s'eteint. C'est le son le plus long du jeu apres la fanfare, et le seul
    son grave a etre tenu : il doit passer par-dessus la bataille qui vient de l'emporter, la ou
    attack_taken (plus sec, plus court) ne marque qu'un coup encaisse.
    """
    toll = mix(bell(196, 0.55, brightness=0.12, curve=1.6),
               at(gain(bell(147, 0.62, brightness=0.10, curve=1.4), 0.9), 0.20))
    rubble = envelope(lowpass(noise(0.78), 420), attack=0.03, curve=1.8)
    sub = envelope(tone(82, 0.70, "sine", freq_end=41), attack=0.02, curve=2.0)
    return finish(lowpass(mix(toll, gain(rubble, 0.45), gain(sub, 0.6)), 2200), peak=0.7)


# toast_warning n'y figure pas : c'est le seul son importe, ce script ne doit pas l'ecraser.
SOUNDS = {
    "toast_info": toast_info,
    "toast_victory": toast_victory,
    "toast_loss": toast_loss,
    "achievement": achievement,
    "harvest_manual": harvest_manual,
    "attack_dealt": attack_dealt,
    "attack_taken": attack_taken,
    "city_founded": city_founded,
    "city_lost": city_lost,
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
