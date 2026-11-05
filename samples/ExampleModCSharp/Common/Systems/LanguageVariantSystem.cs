using Terraria;
using Terraria.Localization;
using Terraria.ModLoader;

namespace ExampleMod.Common.Systems
{

    public class LanguageVariantSystem : ModSystem
    {
        
        public override void PostSetupContent() {
            
        }

        public void VariationFeatureShowcase() {
            var nouns = Language.FindAll(Lang.CreateDialogFilter("Mods.ExampleMod.VariationShowcase.Noun"));
            (string key, string variationKeyword, bool plural)[] messages = [
                ("Mods.ExampleMod.VariationShowcase.LooksNiceMessage", variationKeyword: "Gender", plural: false),
                ("Mods.ExampleMod.VariationShowcase.CollectiveNounMessage", variationKeyword: "Collective", plural: true)
            ];
            foreach (var message in messages) {
                foreach (var noun in nouns) {
                    string result = GetVariantMessageForNoun(message.key, noun.Key, message.variationKeyword, message.plural);
                    Mod.Logger.Info($"VariationFeatureShowcase.cs: {result}");
                }
            }
        }

        public static string GetVariantMessageForNoun(string messageKey, string nounKey, string variationKeyword, bool plural) {
            LocalizedText nounText = Language.GetText(nounKey);
            LocalizedText messageText = Language.GetText(messageKey);
            string messageTextValue = messageText.Value;

            if (Language.TryGetVariation(nounText.Key, variationKeyword, out var nounVariation) && Language.TryGetVariation(messageText.Key, nounVariation, out var messageVariation))
                messageTextValue = messageVariation;

            string pluralizedNoun = nounText.Format(!plural ? 1 : 2);
            return LocalizedText.Literal(messageTextValue).Format(pluralizedNoun);
        }
    }
}
