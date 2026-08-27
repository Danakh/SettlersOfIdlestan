namespace SettlersOfIdlestan.Model.Game
{
    /// <summary>
    /// Calcule combien de cycles complets d'un cooldown périodique se sont écoulés entre deux ticks,
    /// pour les comportements de production/consommation abonnés à <see cref="GameClock.Advanced"/>.
    ///
    /// <para>Sans ceci, le pattern historique (<c>if (now - lastTick &lt; cooldown) return; ... lastTick = now;</c>)
    /// n'agit qu'une fois par événement <c>Advanced</c>, quelle que soit la durée écoulée. En jeu
    /// normal l'écart entre deux événements reste petit (une fraction de seconde), donc ça ne se
    /// voit pas. Mais un saut de temps (voir <c>TimeJumpService</c>) ne déclenche qu'un seul
    /// événement par tranche de 10 000 ticks (100 s de jeu) : un cooldown de quelques centaines de
    /// ticks se retrouve alors bridé à une seule exécution là où il aurait dû s'exécuter des
    /// dizaines de fois, sous-simulant fortement la production/consommation pendant le saut.</para>
    /// </summary>
    public static class TickCooldown
    {
        /// <summary>
        /// Retourne le nombre de cycles de <paramref name="cooldownTicks"/> écoulés depuis
        /// <paramref name="lastActionTick"/> jusqu'à <paramref name="now"/>, et avance
        /// <paramref name="lastActionTick"/> d'autant de cycles (pas jusqu'à <paramref name="now"/> :
        /// le reliquat de ticks sous le seuil du prochain cycle est conservé pour l'appel suivant,
        /// au lieu d'être perdu).
        ///
        /// <para><paramref name="lastActionTick"/> à 0 est traité comme une valeur normale (élapsed =
        /// <paramref name="now"/>) par défaut : c'est le comportement historique de la quasi-totalité
        /// des sites convertis à cet utilitaire (un tracker jamais déclenché valait 0 et le premier
        /// contrôle agissait dès que `now &gt;= cooldown`, sans délai d'amorçage). Seuls quelques sites
        /// avaient explicitement un garde-fou <c>if (lastTick == 0) { lastTick = now; continue/return; }</c>
        /// dans le code d'origine (ex. <c>ResearchController</c>) — passer <paramref name="coldStartOnZero"/>
        /// à true UNIQUEMENT pour reproduire ce comportement précis, jamais par défaut.</para>
        /// </summary>
        public static long ConsumeElapsedCycles(long now, ref long lastActionTick, long cooldownTicks, bool coldStartOnZero = false)
        {
            if (cooldownTicks <= 0) cooldownTicks = 1;

            if (coldStartOnZero && lastActionTick == 0)
            {
                lastActionTick = now;
                return 0;
            }

            long elapsed = now - lastActionTick;
            if (elapsed < cooldownTicks) return 0;

            long cycles = elapsed / cooldownTicks;
            lastActionTick += cycles * cooldownTicks;
            return cycles;
        }
    }
}
