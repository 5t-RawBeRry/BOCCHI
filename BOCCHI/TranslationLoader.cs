using BOCCHI.Common.Config;
using Ocelot.Lifecycle;
using Ocelot.Services.Translation;

namespace BOCCHI;

public class TranslationLoader(ITranslationRepository translations, UIConfig config) : IOnStart, IOnUpdate
{
    private string? activeLanguage;

    public void OnStart()
    {
        translations.LoadFromDirectory("Translations", "en");
        translations.LoadFromDirectory("Translations", "jp");
        translations.LoadFromDirectory("Translations", "ko");
        ApplyConfiguredLanguage();
    }

    public void Update()
    {
        ApplyConfiguredLanguage();
    }

    private void ApplyConfiguredLanguage()
    {
        string language = config.Language.TranslationCode();
        if (language == activeLanguage && translations.CurrentLanguage == language)
        {
            return;
        }

        translations.SetLanguage(language);
        activeLanguage = language;
    }
}
