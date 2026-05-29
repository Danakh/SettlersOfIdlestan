using System.Linq;
using SettlersOfIdlestan.Model.Game;
using SettlersOfIdlestan.Model.Tasks;
using SettlersOfIdlestanSkia.Renderers.Overlay;

namespace SettlersOfIdlestanSkia.Services;

public class TutorialService
{
    private readonly TutorialRenderer _tutorialRenderer;

    public TutorialService(TutorialRenderer tutorialRenderer)
    {
        _tutorialRenderer = tutorialRenderer;
    }

    public void InitializeForNewGame(MainGameState state)
    {
        state.TutorialStepIndex = 0;
        RefreshStep(state);
    }

    public void InitializeForLoadedGame(MainGameState state)
    {
        RefreshStep(state);
    }

    public void Update(MainGameState state)
    {
        var index = state.TutorialStepIndex;

        var steps = TutorialStepDefinitions.All;
        if (index >= steps.Count)
        {
            _tutorialRenderer.SetStep(null);
            return;
        }

        var step = steps[index];
        var gameRecord = state.GameRecord;
        var runRecord = state.CurrentIslandState?.RunRecord;

        if (step.PrimaryTasks.All(t => t.IsCompleted(gameRecord, runRecord)))
        {
            state.TutorialStepIndex = index + 1;
            RefreshStep(state);
        }
    }

    private void RefreshStep(MainGameState state)
    {
        var steps = TutorialStepDefinitions.All;
        if (state.TutorialStepIndex < steps.Count)
            _tutorialRenderer.SetStep(steps[state.TutorialStepIndex]);
        else
            _tutorialRenderer.SetStep(null);
    }
}
