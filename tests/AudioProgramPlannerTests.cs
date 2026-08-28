using Tractus.PresenterTest;
using Xunit;

namespace TractusPresenterTest.Tests;

public sealed class AudioProgramPlannerTests
{
    private static readonly AudioMode[] IndividualModes =
    [
        AudioMode.Tone, AudioMode.Silence, AudioMode.NoAudio, AudioMode.Silence,
        AudioMode.Tone, AudioMode.Silence, AudioMode.NoAudio, AudioMode.Tone
    ];

    [Fact]
    public void IndividualModeUsesEachPresentersSetting()
    {
        for (var number = 1; number <= IndividualModes.Length; number++)
            Assert.Equal(IndividualModes[number - 1], Resolve(AudioProgramMode.Individual, number));
    }

    [Theory]
    [InlineData(AudioProgramMode.AllTones, AudioMode.Tone)]
    [InlineData(AudioProgramMode.SilenceAll, AudioMode.Silence)]
    public void GlobalModesApplyToEveryPresenter(AudioProgramMode program, AudioMode expected)
    {
        for (var number = 1; number <= IndividualModes.Length; number++)
            Assert.Equal(expected, Resolve(program, number));
    }

    [Fact]
    public void ChaseEnablesExactlyOneTone()
    {
        var modes = Enumerable.Range(1, 8)
            .Select(number => Resolve(AudioProgramMode.ToneChase, number, chase: 6))
            .ToArray();

        Assert.Equal(1, modes.Count(mode => mode == AudioMode.Tone));
        Assert.Equal(AudioMode.Tone, modes[5]);
        Assert.All(modes.Where((_, index) => index != 5), mode => Assert.Equal(AudioMode.Silence, mode));
    }

    [Theory]
    [InlineData(1, 2)]
    [InlineData(7, 8)]
    [InlineData(8, 1)]
    public void ChaseWrapsAround(int current, int expected) =>
        Assert.Equal(expected, AudioProgramPlanner.NextChasePresenter(current, 8));

    private static AudioMode Resolve(AudioProgramMode program, int presenter, int chase = 1) =>
        AudioProgramPlanner.Resolve(program, presenter, chase, IndividualModes);
}
