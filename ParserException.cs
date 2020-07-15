using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OdeSolverWPF
{
    internal enum Situation
    {
        NO_TEXT,
        NO_BEGIN,
        NO_OPEN_FBRACKET,
        NO_CLOSE_FBRACKET,
        NO_EQUATIONS,
        NO_EQU_MARK,
        NO_EQUATION_NAME,
        EQU_IDENT_NOTMATCH,
        NO_DOTCOMMA
    }
    class ParserException : Exception
    {
        private String _message;

        public override string Message
        {
            get
            {
                return _message;
            }
        }
        public ParserException(String S)
        {
            _message = S;
        }
        public ParserException(Situation S)
        {
            switch (S)
            {
                case Situation.NO_TEXT:
                    _message = @"Текст отсутствует.";
                    break;
                case Situation.NO_BEGIN:
                    _message = @"Ожидалось начало конструкции языка 'Execute'";
                    break;
                case Situation.NO_OPEN_FBRACKET:
                    _message = @"Отсутствует открывающая скобка '{'";
                    break;
                case Situation.NO_CLOSE_FBRACKET:
                    _message = @"Отсутвует закрывающая скобка '}'";
                    break;
                case Situation.NO_EQUATIONS:
                    _message = @"Ожидалось объявление уравнений 'Equations'";
                    break;
                case Situation.NO_EQUATION_NAME:
                    _message = "Отсутствует имя уравнений после их объявления";
                    break;
                case Situation.EQU_IDENT_NOTMATCH:
                    _message = @"Имя идентификатора уравнения не соответствует объявленному в 'Equations'";
                    break;
                case Situation.NO_EQU_MARK:
                    _message = @"Отсутствует знак равенства";
                    break;
                case Situation.NO_DOTCOMMA:
                    _message = @"Отсутствует точка с запятой в конце предыдущего выражения";
                    break;
                default:
                    _message = @"Ошибка не поддерживается программой";
                    break;
            }
        }

    }
}
