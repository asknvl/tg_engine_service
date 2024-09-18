using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace tg_engine.translator
{
    public interface ITranslator
    {
        Task<string> translate(string text);
    }
}
