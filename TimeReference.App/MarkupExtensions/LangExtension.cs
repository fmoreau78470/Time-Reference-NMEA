using System;
using System.Windows.Data;
using System.Windows.Markup;
using TimeReference.Core.Services;

namespace TimeReference.App.MarkupExtensions
{
    [MarkupExtensionReturnType(typeof(string))]
    public class LangExtension : MarkupExtension
    {
        public string Key { get; set; }

        public LangExtension(string key)
        {
            Key = key;
        }

        public override object ProvideValue(IServiceProvider serviceProvider)
        {
            // Crée un Binding dynamique sur l'indexeur du TranslationManager
            var binding = new Binding($"[{Key}]")
            {
                Source = TranslationManager.Instance,
                Mode = BindingMode.OneWay
            };
            return binding.ProvideValue(serviceProvider);
        }
    }
}