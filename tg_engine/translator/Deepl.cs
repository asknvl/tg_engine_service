﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using tg_engine.config;
using DeepL;
using logger;

namespace tg_engine.translator
{
    public class Deepl : ITranslator
    {
        #region vars
        settings_translator settings;        
        Translator translator;
        ILogger logger;
        #endregion

        public Deepl(settings_translator settings, ILogger logger) {

            this.settings = settings;
            this.logger = logger;

            translator = new Translator(settings.api_key);
        }

        public async Task<string> translate_incoming(string text)
        {
            string res = "";

            if (string.IsNullOrEmpty(text))
                return text;

            try
            {
                string[] arr = { text };
                var tr = await translator.TranslateTextAsync(arr, null, "RU");
                res = tr[0].Text;

                logger.dbg("deepl", $"{tr[0].Text}\n{tr[0].DetectedSourceLanguageCode}");

            } catch (Exception ex)
            {
                logger.err("deepl", $"translate_incoming: {text} {ex.Message}");    
                res = text;
            }

            return res; 
        }
            

        public string translate_outcoming(string text)
        {
            return text;
        }
    }
}
