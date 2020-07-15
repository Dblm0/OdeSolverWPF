using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Linq.Expressions;

namespace OdeSolverWPF
{

    class Parser
    {
        internal enum TerminalId
        {
            EXECUTE,
            EQUATIONS,
            OPEN_F_BRACKET,
            CLOSE_F_BRACKET,
            OPEN_S_BRACKET,
            CLOSE_S_BRACKET,
            OPEN_R_BRACKET,
            CLOSE_R_BRACKET,
            COMMA,
            DOT_COMMA,
            DOT_DOT,
            EQU,
            PLUS,
            MINUS,
            PROD,
            DIV,
            POW,
            FUNC,
            METHOD,
            PLOT,
            TABLE,
            VARIABLE,
            NUMBER,
            UNKNOWN
        }

        private Dictionary<String, TerminalId> ReservedTerminals;
        private Dictionary<String, Double> Variables;
        private Dictionary<String, Double[]> ArrayVariables;
        private Dictionary<String, Func<Double, Double>> Functions;
        private Dictionary<String, System.Reflection.MethodInfo> OdeFunctions;

        private delegate Double[][] OdeDelegate(Func<Double, Double[], Double[]> F, Double[] T, Double[] X0);
        private Dictionary<String, OdeDelegate> OdeMethods;
        private Dictionary<String, Double[][]> OdeSolutions;

        private List<Expression> OdeList;


        private ParameterExpression X;
        ParameterExpression T = Expression.Parameter(typeof(Double), "t");
        private List<TerminalId> _terminalIds;

        private int _lexemposition;
        private int _carretposition;

        private List<String> _lexems;


        //Необработанный текст
        private String _rawtext;

        //Имя дифура (ожидается формат d\w+dt)
        private String _equName;
        //Имя дифференцируемой переменной, извлеченное из d\w+dt
        private String _equVarName;
        //Переменная-флаг, отвечающая за тип уравнения: дифур или константа 
        private Boolean _isСonstant;

        //Индексы правой части диффура
        private List<Int32> RPIndexes;

        //Индексы левой части диффура
        private List<Int32> LPIndexes;



        public Parser()
        {
            #region Dictionaries INIT
            ReservedTerminals = new Dictionary<String, TerminalId>
            {
                {"execute",TerminalId.EXECUTE},
                {"equations",TerminalId.EQUATIONS},

                {"{",TerminalId.OPEN_F_BRACKET},
                {"}",TerminalId.CLOSE_F_BRACKET},

                {"[",TerminalId.OPEN_S_BRACKET},
                {"]",TerminalId.CLOSE_S_BRACKET},

                {"(",TerminalId.OPEN_R_BRACKET},
                {")",TerminalId.CLOSE_R_BRACKET},

                {",",TerminalId.COMMA},
                {";",TerminalId.DOT_COMMA},
                {":",TerminalId.DOT_DOT},
                {"=",TerminalId.EQU},
                
                {"-",TerminalId.MINUS},
                {"+",TerminalId.PLUS},

                {"*",TerminalId.PROD},
                {"/",TerminalId.DIV},

                {"^",TerminalId.POW},

                {"sin",TerminalId.FUNC},
                {"cos",TerminalId.FUNC},
                {"exp",TerminalId.FUNC},
                {"abs",TerminalId.FUNC},

                {"euler",TerminalId.METHOD},
                {"eulercauchy",TerminalId.METHOD},
                {"rk2",TerminalId.METHOD},
                {"rk4",TerminalId.METHOD},
                {"rkf45",TerminalId.METHOD},

                {"plot",TerminalId.PLOT},
                {"table",TerminalId.TABLE},
            };

            OdeMethods = new Dictionary<string, OdeDelegate>
            {
                {"euler", OdeSolver.Euler},
                {"eulercauchy",OdeSolver.EulerCauchy},
                {"rk2",OdeSolver.RK2},
                {"rk4",OdeSolver.RK4},
                {"rkf45",OdeSolver.RKF45},
            };

            Functions = new Dictionary<String, Func<Double, Double>>
            {
                {"sin", x=> Math.Sin(x)},
                {"cos", x=> Math.Cos(x)},
                {"exp", x=> Math.Exp(x)},
                {"abs", x => Math.Abs(x)}
            };
            OdeFunctions = new Dictionary<String, System.Reflection.MethodInfo>()
            {
                {"sin", typeof(Math).GetMethod("Sin")},
                {"cos", typeof(Math).GetMethod("Cos")},
                {"exp", typeof(Math).GetMethod("Exp")},
                {"abs", typeof(Math).GetMethod("Abs",new Type[]{typeof(Double)})}
            };

            Variables = new Dictionary<String, Double>();
            Variables.Add("pi", Math.PI);

            ArrayVariables = new Dictionary<String, Double[]>();
            OdeSolutions = new Dictionary<String, Double[][]>();
            #endregion

            OdeList = new List<Expression>();
            LPIndexes = new List<Int32>();
            RPIndexes = new List<Int32>();
        }


        #region Misc

        private TerminalId Ident(String Input)
        {
            TerminalId Result;
            if (ReservedTerminals.TryGetValue(Input.ToLower(), out Result))
                return Result;
            else
            {
                if (IsNum(Input))
                    return TerminalId.NUMBER;

                if (IsVariable(Input))
                    return TerminalId.VARIABLE;

                return TerminalId.UNKNOWN;
            }
        }
        private List<String> SplitString(String Input)
        {
            // Add spaces to pattern symbols
            String Pattern = @"[-+*/=^:;,(){}]|\[|\]";
            String Target = " $0 ";
            Input = Regex.Replace(Input, Pattern, Target);

            // Split string to lexems
            char[] Separators = { ' ', '\n', '\r', '\t' };
            String[] Buf = Input.Split(Separators, StringSplitOptions.RemoveEmptyEntries);

            return new List<String>(Buf);
        }
        private bool IsVariable(String Input)
        {
            String Pattern = "^[a-z][a-z|0-9]*$";
            return Regex.IsMatch(Input, Pattern, RegexOptions.IgnoreCase);
        }

        private bool IsNum(String Input)
        {
            String Pattern = "^[0-9]+([.][0-9]+)?$";
            return Regex.IsMatch(Input, Pattern, RegexOptions.IgnoreCase);
        }

        private bool TryGetDifName(String Input, out String Name)
        {
            Regex R = new Regex(@"^d(?<Name>\w+)dt$");
            try
            {
                Name = R.Match(Input).Result("${Name}");
                return true;
            }
            catch (Exception)
            {
                Name = String.Empty;
                return false;
            }

        }

        private Double TryGetVar(String VarName)
        {
            if (Variables.ContainsKey(VarName))
                return Variables[VarName];
            else
                throw new ParserException(String.Format("Переменная \"{0}\" не объявлена", VarName));
        }
        private Double[] TryGetArrayVar(String VarName)
        {
            if (ArrayVariables.ContainsKey(VarName))
                return ArrayVariables[VarName];
            else
                throw new ParserException(String.Format("Переменная \"{0}\" не объявлена", VarName));
        }

        private Double GetValue(TerminalId Terminal)
        {
            switch (Terminal)
            {
                case TerminalId.VARIABLE:
                    return TryGetVar(_lexems[_lexemposition]);

                case TerminalId.NUMBER:
                    return Convert.ToDouble(_lexems[_lexemposition],
                        new System.Globalization.CultureInfo("en-US"));
                default:
                    throw new ParserException("Ожидалась переменная или число");
            }
        }

        #endregion

        #region Main Logic

        public ErrorWord GetError()
        {
            if (_lexems == null || _lexemposition > _lexems.Count - 1)
                return new ErrorWord(0, String.Empty);

            String Word = _lexems[_lexemposition];

            return new ErrorWord(_carretposition, Word);
        }

        private void ResetPosition()
        {
            _lexemposition = 0;
            _carretposition = 0;
        }

        private bool TryMoveToNext()
        {
            _lexemposition++;
            try
            {
                String CurWord = _lexems[_lexemposition];
                _carretposition = _rawtext.IndexOf(CurWord, _carretposition + 1);
                return true;
            }
            catch (ArgumentOutOfRangeException)
            {
                return false;
            }
        }

        public void Execute(String Input)
        {
            _rawtext = Input;
            Execute(SplitString(Input));
        }

        private void Execute(List<String> Lexems)
        {
            if (Lexems.Count == 0)
                throw new ParserException(Situation.NO_TEXT);

            _terminalIds = new List<TerminalId>();
            _lexems = Lexems;

            foreach (String Word in _lexems)
                _terminalIds.Add(Ident(Word));

            ResetPosition();

            //Проверка на наличие терминала Execute
            if (_terminalIds[_lexemposition] != TerminalId.EXECUTE)
                throw new ParserException(Situation.NO_BEGIN);

            if (!TryMoveToNext())
                throw new ParserException(Situation.NO_OPEN_FBRACKET);

            //Проверка на наличие открывающей фигурной скобки
            if (_terminalIds[_lexemposition] != TerminalId.OPEN_F_BRACKET)
                throw new ParserException(Situation.NO_OPEN_FBRACKET);
            else
                if (!TryMoveToNext())
                    throw new ParserException("Основной блок отсутствует");

            //Проверка основного блока
            MainBlock();

            //Проверка на наличие закрывающей фигурной скобки
            if (_terminalIds[_lexemposition] != TerminalId.CLOSE_F_BRACKET)
                throw new ParserException(Situation.NO_CLOSE_FBRACKET);

        }

        private void MainBlock()
        {
            Equations();

            Solution();
        }

        private void Equations()
        {
            //Проверка на наличие терминала Equations
            if (_terminalIds[_lexemposition] != TerminalId.EQUATIONS)
                throw new ParserException(Situation.NO_EQUATIONS);
            else
                if (!TryMoveToNext())
                    throw new ParserException("Отсутствует объявление имени дифференциального уравнения");

            //Проверка на имя уравнений
            if (_terminalIds[_lexemposition] != TerminalId.VARIABLE)
            {
                switch (_terminalIds[_lexemposition])
                {
                    case TerminalId.OPEN_F_BRACKET:
                        throw new ParserException("Отсутствует имя переменной уравнений");
                    case TerminalId.EXECUTE:
                    case TerminalId.EQUATIONS:
                    case TerminalId.FUNC:
                    case TerminalId.METHOD:
                        throw new ParserException("Недопустимо использовать зарезервированное слово в качестве идентификатора переменной");

                    default:
                        throw new ParserException("Ожидалось имя переменной уравнений");
                }
            }
            else
            {
                _equName = _lexems[_lexemposition];
                if (!TryGetDifName(_equName, out _equVarName))
                    throw new ParserException("Неверный формат идентификатора уравнений");

                X = Expression.Parameter(typeof(Double[]), _equVarName);

                if (!TryMoveToNext())
                    throw new ParserException(Situation.NO_OPEN_FBRACKET);
                //Проверка на наличие открывающей фигурной скобки
                if (_terminalIds[_lexemposition] != TerminalId.OPEN_F_BRACKET)
                    throw new ParserException(Situation.NO_OPEN_FBRACKET);

                if (!TryMoveToNext())
                    throw new ParserException("Уравнения отсутствуют");

                //Проверка блока уравнений 
                while (_terminalIds[_lexemposition] != TerminalId.CLOSE_F_BRACKET)
                    EquBlock();

                //Проверка на наличие закрывающей фигурной скобки
                if (_terminalIds[_lexemposition] != TerminalId.CLOSE_F_BRACKET)
                    throw new ParserException(Situation.NO_CLOSE_FBRACKET);

                var NotEq = from lp in LPIndexes
                            from rp in RPIndexes
                            where rp != lp
                            select rp;

                foreach (int i in LPIndexes)
                {
                    RPIndexes.Remove(i);
                }
                if (RPIndexes.Count != 0)
                    throw new ParserException("В правой части дифференциального уравнения неизвестные дифференцируемые переменные");

                if (!TryMoveToNext())
                    throw new ParserException("Отсутствует основная часть программы");
            }
        }
        private void EquBlock()
        {

            //Проверка переменной
            String CurVar = _lexems[_lexemposition];
            if (_terminalIds[_lexemposition] == TerminalId.VARIABLE)
            {
                if (CurVar == _equName)
                {
                    _isСonstant = false;
                    if (!TryMoveToNext())
                        throw new ParserException("Отсутствует начало индексации [");

                    if (_terminalIds[_lexemposition] != TerminalId.OPEN_S_BRACKET)
                        throw new ParserException("Отсутствует начало индексации [");

                    if (!TryMoveToNext())
                        throw new ParserException("Отсутствует индекс");

                    if (_terminalIds[_lexemposition] != TerminalId.NUMBER)
                        throw new ParserException("В качестве индекса допустимо использовать только целые числа");

                    Int32 index = Convert.ToInt32(_lexems[_lexemposition]);
                    if (!LPIndexes.Contains(index))
                        LPIndexes.Add(index);

                    if (!TryMoveToNext())
                        throw new ParserException("Отсутствует закрывающая скобка ]");

                    if (_terminalIds[_lexemposition] != TerminalId.CLOSE_S_BRACKET)
                        throw new ParserException("Отсутствует закрывающая скобка ]");

                }
                else
                {
                    if (CurVar == _equVarName)
                    {
                        throw new ParserException("Присвоение в дифференцируемую переменную недопустимо");
                    }
                    else
                    {
                        _isСonstant = true;

                        if (_terminalIds[_lexemposition] == TerminalId.OPEN_S_BRACKET)
                            throw new ParserException(String.Format("Переменная {0} не может быть массивом в данном контексте", CurVar));
                    }

                }
            }
            else
            {
                throw new ParserException("Неверный формат переменной");
            }

            // Проверка знака раваенства
            if (!TryMoveToNext())
                throw new ParserException(Situation.NO_EQU_MARK);

            if (_terminalIds[_lexemposition] == TerminalId.EQU)
            {
                if (!TryMoveToNext())
                    throw new ParserException("Отсутствует правая часть выражения");

                if (_terminalIds[_lexemposition] == TerminalId.DOT_COMMA)
                    throw new ParserException("Отсутствует правая часть выражения");

                if (_isСonstant)
                    Variables[CurVar] = GetRightPart();
                else
                    OdeList.Add(GetOdeRightPart());

            }
            else throw new ParserException(Situation.NO_EQU_MARK);

            //Проверка точки с запятой
            if (_terminalIds[_lexemposition] != TerminalId.DOT_COMMA)
                throw new ParserException(Situation.NO_DOTCOMMA);

            if (!TryMoveToNext())
                throw new ParserException("Отсутствует закрывающая скобка }");

        }
        private void Solution()
        {
            Variables.Clear();
            Variables.Add("pi", Math.PI);

            String VarName;

            do
            {
                switch (_terminalIds[_lexemposition])
                {
                    case TerminalId.PLOT:
                    case TerminalId.TABLE:
                        Display();
                        break;
                    case TerminalId.VARIABLE:
                        if (IsParamBegin(out VarName))
                            Param(VarName);
                        else
                            Method(VarName);
                        break;
                    default:
                        break;
                }
                if (!TryMoveToNext())
                    throw new ParserException("Отсутствует закрывающая скобка }");

            } while (_terminalIds[_lexemposition] != TerminalId.CLOSE_F_BRACKET);

        }

        private bool IsParamBegin(out String VariableName)
        {
            if (_terminalIds[_lexemposition] != TerminalId.VARIABLE)
                throw new ParserException("Ожидалось объявление переменной-параметра");

            VariableName = _lexems[_lexemposition];

            if (!TryMoveToNext())
                throw new ParserException(Situation.NO_EQU_MARK);

            if (_terminalIds[_lexemposition] != TerminalId.EQU)
                throw new ParserException(Situation.NO_EQU_MARK);

            if (!TryMoveToNext())
                throw new ParserException("Отсутствует правая часть");

            if (_terminalIds[_lexemposition] == TerminalId.METHOD)
                return false;
            else
                return true;

        }
        private void Param(String VarName)
        {

            if (_terminalIds[_lexemposition] != TerminalId.OPEN_S_BRACKET)
            {
                Variables[VarName] = GetRightPart();
            }
            else
            {
                if (!TryMoveToNext())
                    throw new ParserException("Отсутствует содержимое параметра");
                Double Param;

                Param = GetValue(_terminalIds[_lexemposition]);

                if (!TryMoveToNext())
                    throw new ParserException("Отсутствует разделитель");

                switch (_terminalIds[_lexemposition])
                {
                    case TerminalId.DOT_DOT:
                        ArrayVariables[VarName] = SpanParam(Param);
                        break;
                    case TerminalId.COMMA:
                        ArrayVariables[VarName] = ArrayParam(Param);
                        break;
                    default:
                        throw new ParserException("Отсутствует разделитель между содержимым параметра");
                }
                if (!TryMoveToNext())
                    throw new ParserException(Situation.NO_DOTCOMMA);
            }

            if (_terminalIds[_lexemposition] != TerminalId.DOT_COMMA)
                throw new ParserException(Situation.NO_DOTCOMMA);
        }
        private Double[] SpanParam(Double StartParam)
        {
            if (!TryMoveToNext())
                throw new ParserException("Отсутствует значение");

            Double Step;
            Step = GetValue(_terminalIds[_lexemposition]);
            if (!TryMoveToNext())
                throw new ParserException("Отсутствует двоеточие");

            if (_terminalIds[_lexemposition] != TerminalId.DOT_DOT)
                throw new ParserException("Отсутствует двоеточие");

            if (!TryMoveToNext())
                throw new ParserException("Отсутствует значение");

            Double EndParam;
            EndParam = GetValue(_terminalIds[_lexemposition]);

            if (!TryMoveToNext())
                throw new ParserException("Отсутствует закрывающая скобка ]");

            if (_terminalIds[_lexemposition] != TerminalId.CLOSE_S_BRACKET)
                throw new ParserException("Отсутствует закрывающая скобка ]");

            Double StepNumber = (EndParam - StartParam) / Step;
            Int32 N = Convert.ToInt32(Math.Floor(StepNumber));

            Double[] Span = Enumerable.Range(0, N + 1).Select(x => x * Step + StartParam).ToArray();
            return Span;
        }
        private Double[] SpanParam()
        {
            if (_terminalIds[_lexemposition] != TerminalId.OPEN_S_BRACKET)
                throw new ParserException("Остутствует открывающая скобка [");

            if (!TryMoveToNext())
                throw new ParserException("Отсутствует значение");

            Double Param;
            Param = GetValue(_terminalIds[_lexemposition]);

            if (!TryMoveToNext())
                throw new ParserException("Отсутствует двоеточие]");

            return SpanParam(Param);
        }
        private Double[] ArrayParam(Double FirstElement)
        {
            if (!TryMoveToNext())
                throw new ParserException("Отсутствует значение");

            List<Double> InitialConditions = new List<Double>();
            InitialConditions.Add(FirstElement);

            InitialConditions.Add(GetValue(_terminalIds[_lexemposition]));

            if (!TryMoveToNext())
                throw new ParserException("Отсутствует запятая");

            while (_terminalIds[_lexemposition] != TerminalId.CLOSE_S_BRACKET)
            {
                if (_terminalIds[_lexemposition] != TerminalId.COMMA)
                    throw new ParserException("Ожидалась запятая или закрывающая скобка ]");

                if (!TryMoveToNext())
                    throw new ParserException("Отсутствует значение");

                InitialConditions.Add(GetValue(_terminalIds[_lexemposition]));
                if (!TryMoveToNext())
                    throw new ParserException("Отсутствует закрывающая скобка ]");

            }
            return InitialConditions.ToArray();

        }

        private Double[] ArrayParam()
        {
            if (_terminalIds[_lexemposition] != TerminalId.OPEN_S_BRACKET)
                throw new ParserException("Остутствует открывающая скобка [");

            if (!TryMoveToNext())
                throw new ParserException("Отсутствует значение");

            Double Param;
            Param = GetValue(_terminalIds[_lexemposition]);

            if (!TryMoveToNext())
                throw new ParserException("Отсутствует запятая");

            return ArrayParam(Param);
        }
        private void Method(String VarName)
        {
            String MethodName = _lexems[_lexemposition];

            if (!TryMoveToNext())
                throw new ParserException("Отсутствует открывающая скобка (");

            if (_terminalIds[_lexemposition] != TerminalId.OPEN_R_BRACKET)
                throw new ParserException(Situation.NO_OPEN_FBRACKET);

            if (!TryMoveToNext())
                throw new ParserException("Отсутствует параметр");

            if (_lexems[_lexemposition] != _equName)
                throw new ParserException("В качестве первого параметра необходимо передать имя дифференциального уравнения");

            if (!TryMoveToNext())
                throw new ParserException("Отсутствует запятая");


            if (_terminalIds[_lexemposition] != TerminalId.COMMA)
                throw new ParserException("Ожидалась запятая");

            if (!TryMoveToNext())
                throw new ParserException("Отсутствует параметр");

            Double[] Timespan;
            switch (_terminalIds[_lexemposition])
            {
                case TerminalId.OPEN_S_BRACKET:
                    Timespan = SpanParam();
                    break;
                case TerminalId.VARIABLE:
                    Timespan = TryGetArrayVar(_lexems[_lexemposition]);
                    break;
                default:
                    throw new ParserException("Ожидалось имя переменной или открывающаяся скобка [");
            }

            if (!TryMoveToNext())
                throw new ParserException("Отсутствует запятая");

            if (_terminalIds[_lexemposition] != TerminalId.COMMA)
                throw new ParserException("Ожидалась запятая");

            if (!TryMoveToNext())
                throw new ParserException("Отсутствует параметр");

            Double[] InitialConditions;
            switch (_terminalIds[_lexemposition])
            {
                case TerminalId.OPEN_S_BRACKET:
                    InitialConditions = ArrayParam();
                    break;
                case TerminalId.VARIABLE:
                    InitialConditions = TryGetArrayVar(_lexems[_lexemposition]);
                    break;
                default:
                    throw new ParserException("Ожидалось имя переменной или открывающаяся скобка [");
            }
            Int32 ICL = InitialConditions.Length;
            Int32 LPIC = LPIndexes.Count;
            if (LPIC != ICL)
                throw new ParserException(String.
                    Format("Число начальных условий({0}) не соответствует числу уравнений({1})"
                    , ICL, LPIC));

            if (!TryMoveToNext())
                throw new ParserException("Отсутствует закрывающая скобка )");
            if (_terminalIds[_lexemposition] != TerminalId.CLOSE_R_BRACKET)
                throw new ParserException("Отсутствует закрывающая скобка )");

            if (!TryMoveToNext())
                throw new ParserException(Situation.NO_DOTCOMMA);
            if (_terminalIds[_lexemposition] != TerminalId.DOT_COMMA)
                throw new ParserException(Situation.NO_DOTCOMMA);

            OdeSolutions[VarName] = SolveOde(MethodName, Timespan, InitialConditions);

        }


        #region RightPartParser
        private Double GetRightPart()
        {
            Double RP = 0;
            if (_terminalIds[_lexemposition] == TerminalId.MINUS)
            {
                if (!TryMoveToNext())
                    throw new ParserException("Отсутствует операнд");
                RP = -MulBlock();
            }
            else
            {
                RP = MulBlock();
            }


            TerminalId Operator = _terminalIds[_lexemposition];
            while (Operator == TerminalId.MINUS || Operator == TerminalId.PLUS)
            {
                if (!TryMoveToNext())
                    throw new ParserException("Отсутствует операнд");

                switch (Operator)
                {
                    case TerminalId.MINUS:
                        RP = RP - MulBlock();
                        break;
                    case TerminalId.PLUS:
                        RP = RP + MulBlock();
                        break;
                }
                Operator = _terminalIds[_lexemposition];
            }
            return RP;
        }
        private Double MulBlock()
        {
            Double MB = PowBlock();

            TerminalId Operator = _terminalIds[_lexemposition];
            while (Operator == TerminalId.PROD || Operator == TerminalId.DIV)
            {
                if (!TryMoveToNext())
                    throw new ParserException("Отсутствует операнд");

                switch (Operator)
                {
                    case TerminalId.PROD:
                        MB = MB * PowBlock();
                        break;
                    case TerminalId.DIV:
                        MB = MB / PowBlock();
                        break;
                }
                Operator = _terminalIds[_lexemposition];
            }
            return MB;
        }

        private Double PowBlock()
        {
            Double PB = FuncBlock();

            TerminalId Operator = _terminalIds[_lexemposition];
            while (Operator == TerminalId.POW)
            {
                if (!TryMoveToNext())
                    throw new ParserException("Отсутствует операнд");

                PB = Math.Pow(PB, FuncBlock());

                Operator = _terminalIds[_lexemposition];
            }
            return PB;
        }


        private Double FuncBlock()
        {
            Double FB = 0;

            TerminalId CurrentTerminal = _terminalIds[_lexemposition];

            switch (CurrentTerminal)
            {
                //Разбор функции
                case TerminalId.FUNC:
                    Func<Double, Double> F;
                    Functions.TryGetValue(_lexems[_lexemposition].ToLower(), out F);
                    if (!TryMoveToNext())
                        throw new ParserException("Отсутствует открывающая скобка после функции");

                    if (_terminalIds[_lexemposition] != TerminalId.OPEN_R_BRACKET)
                        throw new ParserException("Отсутствует открывающая скобка после функции");

                    if (!TryMoveToNext())
                        throw new ParserException("Отсутствует аргумент функции");

                    FB = F(GetRightPart());

                    if (_terminalIds[_lexemposition] != TerminalId.CLOSE_R_BRACKET)
                        throw new ParserException("Отсутствует закрывающая скобка после функции");

                    if (!TryMoveToNext())
                        throw new ParserException("Правая часть не завершена");

                    break;

                //Разбор выражения в скобках
                case TerminalId.OPEN_R_BRACKET:
                    if (!TryMoveToNext())
                        throw new ParserException("Правая часть не завершена");

                    FB = GetRightPart();

                    if (_terminalIds[_lexemposition] != TerminalId.CLOSE_R_BRACKET)
                        throw new ParserException("Отсутствует закрывающая скобка");
                    if (!TryMoveToNext())
                        throw new ParserException("Правая часть не завершена");
                    break;

                //Разбор переменной
                case TerminalId.VARIABLE:
                    String Variable = _lexems[_lexemposition];

                    if (Variable == _equVarName)
                        throw new ParserException("Не удается получить значение дифференцируемой переменной");

                    FB = TryGetVar(Variable);

                    if (!TryMoveToNext())
                        throw new ParserException("Отсутствует операнд");

                    if (_terminalIds[_lexemposition] == TerminalId.OPEN_S_BRACKET)
                        throw new ParserException(String.Format("Переменная {0} не является массивом", Variable));
                    break;

                //Разбор числа
                case TerminalId.NUMBER:
                    FB = Convert.ToDouble(_lexems[_lexemposition], new System.Globalization.CultureInfo("en-US"));

                    if (!TryMoveToNext())
                        throw new ParserException("Отсутствует операнд");
                    break;
                default:
                    throw new ParserException("Ожидался операнд");
            }
            return FB;
        }
        #endregion
        #region OdeParser
        private Expression GetOdeRightPart()
        {
            Expression OdeRP;

            if (_terminalIds[_lexemposition] == TerminalId.MINUS)
            {
                if (!TryMoveToNext())
                    throw new ParserException("Отсутствует операнд");
                OdeRP = Expression.Negate(OdeMulBlock());
            }
            else
                OdeRP = OdeMulBlock();


            TerminalId Operator = _terminalIds[_lexemposition];
            while (Operator == TerminalId.MINUS || Operator == TerminalId.PLUS)
            {
                if (!TryMoveToNext())
                    throw new ParserException("Отсутствует операнд");
                switch (Operator)
                {
                    case TerminalId.MINUS:
                        OdeRP = Expression.Subtract(OdeRP, OdeMulBlock());
                        break;
                    case TerminalId.PLUS:
                        OdeRP = Expression.Add(OdeRP, OdeMulBlock());
                        break;
                }
                Operator = _terminalIds[_lexemposition];
            }
            return OdeRP;
        }

        private Expression OdeMulBlock()
        {
            Expression OdeMB = OdePowBlock();

            TerminalId Operator = _terminalIds[_lexemposition];
            while (Operator == TerminalId.PROD || Operator == TerminalId.DIV)
            {
                if (!TryMoveToNext())
                    throw new ParserException("Отсутствует операнд");
                switch (Operator)
                {
                    case TerminalId.PROD:
                        OdeMB = Expression.Multiply(OdeMB, OdePowBlock());
                        break;
                    case TerminalId.DIV:
                        OdeMB = Expression.Divide(OdeMB, OdePowBlock());
                        break;
                }
                Operator = _terminalIds[_lexemposition];
            }
            return OdeMB;
        }


        private Expression OdePowBlock()
        {
            Expression OdePB = OdeFuncBlock();

            TerminalId Operator = _terminalIds[_lexemposition];
            while (Operator == TerminalId.POW)
            {
                if (!TryMoveToNext())
                    throw new ParserException("Отсутствует операнд");
                OdePB = Expression.Power(OdePB, OdeFuncBlock());

                Operator = _terminalIds[_lexemposition];
            }
            return OdePB;
        }

        private Expression OdeFuncBlock()
        {
            Expression OdeFB = Expression.Empty();

            TerminalId CurrentTerminal = _terminalIds[_lexemposition];

            switch (CurrentTerminal)
            {
                case TerminalId.FUNC:
                    System.Reflection.MethodInfo FInfo;
                    OdeFunctions.TryGetValue(_lexems[_lexemposition].ToLower(), out FInfo);
                    if (!TryMoveToNext())
                        throw new ParserException("Отсутствует открывающая скобка после функции");

                    if (_terminalIds[_lexemposition] != TerminalId.OPEN_R_BRACKET)
                        throw new ParserException("Отсутствует открывающая скобка после функции");

                    if (!TryMoveToNext())
                        throw new ParserException("Отсутствует аргумент функции");

                    OdeFB = Expression.Call(FInfo, GetOdeRightPart());

                    if (_terminalIds[_lexemposition] != TerminalId.CLOSE_R_BRACKET)
                        throw new ParserException("Отсутствует закрывающая скобка после функции");

                    if (!TryMoveToNext())
                        throw new ParserException("Правая часть не завершена");

                    break;

                //Разбор выражения в скобках
                case TerminalId.OPEN_R_BRACKET:
                    if (!TryMoveToNext())
                        throw new ParserException("Правая часть не завершена");

                    OdeFB = GetOdeRightPart();

                    if (_terminalIds[_lexemposition] != TerminalId.CLOSE_R_BRACKET)
                        throw new ParserException("Отсутствует закрывающая скобка");
                    if (!TryMoveToNext())
                        throw new ParserException("Правая часть не завершена");
                    break;

                //Разбор переменной
                case TerminalId.VARIABLE:
                    String Variable = _lexems[_lexemposition];

                    if (Variable == _equVarName)
                    {
                        if (!TryMoveToNext())
                            throw new ParserException("Отсутствует открывающая скобка [");
                        OdeFB = OdeArrayEl();

                    }
                    else
                    {
                        if (Variable == "t")
                            OdeFB = T;
                        else
                            OdeFB = Expression.Constant(TryGetVar(Variable), typeof(Double));
                    }

                    if (!TryMoveToNext())
                        throw new ParserException("Правая часть не завершена");
                    if (_terminalIds[_lexemposition] == TerminalId.OPEN_S_BRACKET)
                        throw new ParserException(String.Format("Переменная {0} не является массивом", Variable));

                    break;

                //Разбор числа
                case TerminalId.NUMBER:
                    Double Num = Convert.ToDouble(_lexems[_lexemposition], new System.Globalization.CultureInfo("en-US"));
                    OdeFB = Expression.Constant(Num, typeof(Double));
                    if (!TryMoveToNext())
                        throw new ParserException("Правая часть не завершена");
                    break;

                default:
                    throw new ParserException("Ожидался операнд");
            }
            return OdeFB;
        }

        private Expression OdeArrayEl()
        {
            if (_terminalIds[_lexemposition] != TerminalId.OPEN_S_BRACKET)
                throw new ParserException("Ожидалась [ для начала индексации");

            if (!TryMoveToNext())
                throw new ParserException("Отсутствует индекс");

            if (_terminalIds[_lexemposition] != TerminalId.NUMBER)
                throw new ParserException("Ожидался  целочисленный индекс");

            Int32 index = Convert.ToInt32(_lexems[_lexemposition]);

            if (!TryMoveToNext())
                throw new ParserException("Отсутствует закрывающая скобка ]");

            if (_terminalIds[_lexemposition] != TerminalId.CLOSE_S_BRACKET)
                throw new ParserException("Ожидалась ] для завершения индексации");


            if (!RPIndexes.Contains(index))
                RPIndexes.Add(index);

            return Expression.ArrayAccess(X, Expression.Constant(index));
        }

        #endregion

        #endregion

        #region Solution

        private Double[][] SolveOde(String MethodName, Double[] Timespan, Double[] X0)
        {
            //Get lambda from expressions

            NewArrayExpression NewArr = Expression.NewArrayInit(typeof(Double), OdeList);

            Expression<Func<Double, Double[], Double[]>> lambda =
                Expression.Lambda<Func<Double, Double[], Double[]>>(NewArr, T, X);

            Func<Double, Double[], Double[]> MyOdeFunc = lambda.Compile();

            //Ode solution

            OdeDelegate OdeMethod;
            OdeMethods.TryGetValue(MethodName, out OdeMethod);

            return OdeMethod(MyOdeFunc, Timespan, X0);
        }


        #endregion

        #region Display Methods
        private void Display()
        {

            switch (_terminalIds[_lexemposition])
            {
                case TerminalId.PLOT:
                    PlotDisplay();
                    break;
                case TerminalId.TABLE:
                    TableDisplay();
                    break;
                default:
                    throw new ParserException("Ожидались методы вывода информации на экран plot или table");
            }
            if (!TryMoveToNext())
                throw new ParserException(Situation.NO_DOTCOMMA);

            if (_terminalIds[_lexemposition] != TerminalId.DOT_COMMA)
                throw new ParserException(Situation.NO_DOTCOMMA);
        }
        private void PlotDisplay()
        {
            if (!TryMoveToNext())
                throw new ParserException("Отсутствует открывающая скобка");

            if (_terminalIds[_lexemposition] != TerminalId.OPEN_R_BRACKET)
                throw new ParserException("Отсутствует открывающая скобка");

            Double[] X;
            Double[] Y;
            PlotForm F = new PlotForm();
            String Legend;
            do
            {
                if (!TryMoveToNext())
                    throw new ParserException("Отсутствует параметр");

                List<Double[]> ParamPair = GetParamPair(out Legend);
                X = ParamPair[0];
                Y = ParamPair[1];
                F.AddSeries(Legend, X, Y);

            } while (_terminalIds[_lexemposition] == TerminalId.COMMA);


            if (_terminalIds[_lexemposition] != TerminalId.CLOSE_R_BRACKET)
                throw new ParserException("Отсутствует закрывающая скобка");

            F.Show();

        }
        private void TableDisplay()
        {
            if (!TryMoveToNext())
                throw new ParserException("Отсутствует открывющая скобка");

            if (_terminalIds[_lexemposition] != TerminalId.OPEN_R_BRACKET)
                throw new ParserException("Отсутствует открывающая скобка");

            Double[] X;
            TableForm F = new TableForm();
            String RowName;
            do
            {
                if (!TryMoveToNext())
                    throw new ParserException("Отсутствует параметр");

                X = GetParam(out RowName);

                if (!TryMoveToNext())
                    throw new ParserException("Определение параметров не завершено");

                F.AddRow(RowName, X);
            } while (_terminalIds[_lexemposition] == TerminalId.COMMA);

            F.Show();

        }
        private Double[] GetParam(out String Name)
        {
            if (_terminalIds[_lexemposition] != TerminalId.VARIABLE)
                throw new ParserException("В качестве параметров допустимо использовать только переменные");

            String VarName = _lexems[_lexemposition];

            Double[] Param;
            if (OdeSolutions.ContainsKey(VarName))
            {
                if (!TryMoveToNext())
                    throw new ParserException("Отсутствует открывающая скобка [");

                if (_terminalIds[_lexemposition] != TerminalId.OPEN_S_BRACKET)
                    throw new ParserException("Ожидалось начало индексации [");

                if (!TryMoveToNext())
                    throw new ParserException("Отсутствует индекс");

                if (_terminalIds[_lexemposition] != TerminalId.NUMBER)
                    throw new ParserException("В качестве индекса допустимо использовать только целые числа");

                Int32 index = Convert.ToInt32(_lexems[_lexemposition]);

                if (!LPIndexes.Contains(index))
                    throw new ParserException("Индекс не соответствует размерности переменной");
                Param = OdeSolutions[VarName].Select(x => x[index]).ToArray();

                if (!TryMoveToNext())
                    throw new ParserException("Отсутствует закрывающая скобка ] ");
                if (_terminalIds[_lexemposition] != TerminalId.CLOSE_S_BRACKET)

                    throw new ParserException("Ожидалась закрывающая скобка ]");

                Name = VarName + "[" + index.ToString() + "]";
            }
            else
            {
                Param = TryGetArrayVar(VarName);
                Name = VarName;
            }
            return Param;
        }

        private List<Double[]> GetParamPair(out String PairName)
        {
            if (_terminalIds[_lexemposition] != TerminalId.VARIABLE)
                throw new ParserException("В качестве параметров допустимо использовать только переменные");

            String Name;

            Double[] FirstParam = GetParam(out Name);

            if (!TryMoveToNext())
                throw new ParserException("Отсутствует запятая для описания второго параметра");

            PairName = Name;
            if (_terminalIds[_lexemposition] != TerminalId.COMMA)
                throw new ParserException("Ожидалась запятая для описания второго параметра");

            if (!TryMoveToNext())
                throw new ParserException("Отсутствует параметр");

            Double[] SecondParam = GetParam(out Name);

            if (!TryMoveToNext())
                throw new ParserException("Определение параметров не завершено");

            PairName += ", " + Name;
            if (FirstParam.Length != SecondParam.Length)
                throw new ParserException("Размерности параметров не совпадают");

            List<Double[]> result = new List<double[]>();
            result.Add(FirstParam);
            result.Add(SecondParam);

            return result;

        }


        #endregion




    }


}
