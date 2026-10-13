using System.Collections.Generic;
using ProjectSurvive.Core.Application.Text;
using ProjectSurvive.Core.Domain.Config;
using Xunit;

namespace ProjectSurvive.Core.Tests
{
    /// <summary>
    /// <see cref="LocalizationService"/> is what the Presentation layer is allowed
    /// to call (AGENTS rule 10), so its two promises are tested directly: it
    /// resolves keys per language, and it never invents text for a key that does
    /// not exist.
    /// </summary>
    public sealed class LocalizationServiceTests
    {
        [Fact]
        public void Thai_is_the_default_language()
        {
            var service = new LocalizationService(ConfigFixture.Load());

            Assert.Equal(Language.Th, service.Language);
        }

        [Fact]
        public void Get_returns_the_text_of_the_selected_language()
        {
            IGameConfig config = ConfigFixture.Load();
            var service = new LocalizationService(config, Language.Th);

            string thai = service.Get("ui.generic.ok");
            service.Language = Language.En;
            string english = service.Get("ui.generic.ok");

            Assert.Equal(config.GetText("ui.generic.ok", Language.Th), thai);
            Assert.Equal(config.GetText("ui.generic.ok", Language.En), english);
            Assert.NotEqual(thai, english);
        }

        [Fact]
        public void Get_throws_for_an_unknown_key_instead_of_showing_the_key()
        {
            // A missing string is a data bug (AGENTS rule 5); the UI must not
            // silently display "ui.does.not.exist" to the player.
            var service = new LocalizationService(ConfigFixture.Load());

            Assert.Throws<KeyNotFoundException>(() => service.Get("ui.does.not.exist"));
        }

        [Fact]
        public void TryGet_reports_an_unknown_key_instead_of_throwing()
        {
            var service = new LocalizationService(ConfigFixture.Load());

            Assert.False(service.TryGet("ui.does.not.exist", out string missing));
            Assert.Equal(string.Empty, missing);
            Assert.True(service.TryGet("ui.generic.ok", out string found));
            Assert.False(string.IsNullOrEmpty(found));
        }
    }
}
