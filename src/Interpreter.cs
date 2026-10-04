using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using System.Threading;

namespace BulingOffline
{
    // ============ 值 ============
    public class Value
    {
        public int T;            // 0=int 1=double 2=string
        public long I;
        public double D;
        public string S;
        public static Value Int(long v) { Value x = new Value(); x.T = 0; x.I = v; return x; }
        public static Value Dbl(double v) { Value x = new Value(); x.T = 1; x.D = v; return x; }
        public static Value Str(string v) { Value x = new Value(); x.T = 2; x.S = v; return x; }
        public long AsInt()
        {
            if (T == 0) return I;
            if (T == 1) return (long)D;
            long r; if (long.TryParse((S ?? "").Trim(), NumberStyles.Any, CultureInfo.InvariantCulture, out r)) return r;
            double d2; if (double.TryParse((S ?? "").Trim(), NumberStyles.Any, CultureInfo.InvariantCulture, out d2)) return (long)d2;
            return 0;
        }
        public double AsDbl()
        {
            if (T == 1) return D;
            if (T == 0) return I;
            double d2; if (double.TryParse((S ?? "").Trim(), NumberStyles.Any, CultureInfo.InvariantCulture, out d2)) return d2;
            return 0;
        }
        public bool IsNum() { return T == 0 || T == 1; }
        public string AsStr()
        {
            if (T == 2) return S ?? "";
            if (T == 0) return I.ToString();
            string s = D.ToString("0.0###############", CultureInfo.InvariantCulture);
            return s;
        }
        public double AsNum() { return T == 0 ? I : D; }
        public bool Truthy() { if (T == 0) return I != 0; if (T == 1) return D != 0; return !string.IsNullOrEmpty(S); }
        public Value Clone() { Value x = new Value(); x.T = T; x.I = I; x.D = D; x.S = S; return x; }
    }

    // 变量槽（可单值也可数组）
    public class Slot
    {
        public Value V = Value.Str("");
        public List<Value> Arr;
        public bool IsArray { get { return Arr != null; } }
    }

    public class RefArg
    {
        public Slot Slot;
        public int Index = -1;   // -1 表示整个变量
        public Value Get()
        {
            if (Index < 0) return Slot.V;
            if (Slot.Arr != null && Index < Slot.Arr.Count) return Slot.Arr[Index];
            return Value.Str("");
        }
        public void Set(Value v)
        {
            if (Index < 0) { Slot.V = v; Slot.Arr = null; return; }
            if (Slot.Arr == null) { Slot.Arr = new List<Value>(); Slot.V = Value.Str(""); }
            while (Slot.Arr.Count <= Index) Slot.Arr.Add(Value.Str(""));
            Slot.Arr[Index] = v;
        }
    }

    // ============ 词法 ============
    public enum Tk { Num, Str, Id, Op, Eof }
    public class Token { public Tk T; public string V; public int Line; }

    public class Lexer
    {
        string src; int pos; int line = 1;
        public Lexer(string s) { src = s; }
        static bool IsIdChar(char c)
        {
            return char.IsLetterOrDigit(c) || c == '_' || (c >= 0x4e00 && c <= 0x9fa5) || (c >= 0x3400 && c <= 0x4dbf) || c == '（' || c == '）';
        }
        public List<Token> Lex()
        {
            List<Token> outp = new List<Token>();
            while (pos < src.Length)
            {
                char c = src[pos];
                if (c == '\n') { line++; pos++; continue; }
                if (c == '\r' || c == ' ' || c == '\t') { pos++; continue; }
                if (c == '#')
                {
                    while (pos < src.Length && src[pos] != '\n') pos++;
                    continue;
                }
                if (c == '"' || c == '“')
                {
                    char close = c == '"' ? '"' : '”';
                    pos++;
                    StringBuilder sb = new StringBuilder();
                    while (pos < src.Length && src[pos] != close)
                    {
                        if (src[pos] == '\\' && pos + 1 < src.Length)
                        {
                            char n = src[pos + 1];
                            if (n == 'n') { sb.Append('\n'); pos += 2; continue; }
                            if (n == 't') { sb.Append('\t'); pos += 2; continue; }
                            if (n == '\\') { sb.Append('\\'); pos += 2; continue; }
                            if (n == '"') { sb.Append('"'); pos += 2; continue; }
                            if (n == 'r') { sb.Append('\r'); pos += 2; continue; }
                        }
                        sb.Append(src[pos]); pos++;
                    }
                    pos++; // close
                    Token t = new Token(); t.T = Tk.Str; t.V = sb.ToString(); t.Line = line; outp.Add(t);
                    continue;
                }
                if (char.IsDigit(c) || (c == '.' && pos + 1 < src.Length && char.IsDigit(src[pos + 1])))
                {
                    int st = pos;
                    while (pos < src.Length && (char.IsDigit(src[pos]) || src[pos] == '.')) pos++;
                    Token t = new Token(); t.T = Tk.Num; t.V = src.Substring(st, pos - st); t.Line = line; outp.Add(t);
                    continue;
                }
                if (IsIdChar(c))
                {
                    int st = pos;
                    while (pos < src.Length && IsIdChar(src[pos])) pos++;
                    Token t = new Token(); t.T = Tk.Id; t.V = src.Substring(st, pos - st); t.Line = line; outp.Add(t);
                    continue;
                }
                // 运算符
                string two = pos + 1 < src.Length ? src.Substring(pos, 2) : "";
                if (two == "==" || two == "!=" || two == ">=" || two == "<=" || two == "&&" || two == "||" ||
                    two == "+=" || two == "-=" || two == "*=" || two == "/=" || two == "==")
                {
                    Token t = new Token(); t.T = Tk.Op; t.V = two; t.Line = line; outp.Add(t); pos += 2; continue;
                }
                string ch = c.ToString();
                if ("+-*/%()=<>![],:".IndexOf(c) >= 0)
                {
                    Token t = new Token(); t.T = Tk.Op; t.V = ch; t.Line = line; outp.Add(t); pos++; continue;
                }
                // 未知字符跳过
                pos++;
            }
            Token e = new Token(); e.T = Tk.Eof; e.V = ""; e.Line = line; outp.Add(e);
            return outp;
        }
    }

    // ============ AST ============
    public class ExprNode { public int Kind; public Value Const; public string Name; public ExprNode Idx, A, B; public string Op; public List<ExprNode> Args; public string CallName; public int Line; }
    // Kind: 0=const 1=var 2=var[idx] 3=binary 4=unary- 5=not 6=call

    public class Stmt
    {
        public int Kind;   // 0=expr/assign 1=if 2=while 3=break 4=continue 5=return 6=funcdef 7=include
        public ExprNode Expr;        // 表达式 / include路径 / while条件
        public ExprNode Target;      // 赋值目标（var / var[idx] / call）
        public string AssignOp;
        public List<ExprNode> Conds = new List<ExprNode>();      // if: 每个分支条件
        public List<List<Stmt>> Bodies = new List<List<Stmt>>(); // if分支 / while用[0]
        public List<Stmt> ElseBody;
        public string Name;          // funcdef
        public List<string> Params = new List<string>();
        public int Line;
    }

    public class Script
    {
        public List<Stmt> Body = new List<Stmt>();
        public Dictionary<string, Stmt> Funcs = new Dictionary<string, Stmt>();
    }

    // ============ 信号 ============
    public class BreakSignal : Exception { }
    public class ContinueSignal : Exception { }
    public class ReturnSignal : Exception { public Value V; public ReturnSignal(Value v) { V = v; } }
    public class StopSignal : Exception { }

    // ============ 解析器 ============
    public class Parser
    {
        List<Token> toks; int p;
        Script Sc = new Script();
        public Parser(List<Token> t) { toks = t; }
        Token Peek(int off) { int i = p + off; if (i >= toks.Count) i = toks.Count - 1; return toks[i]; }
        Token Cur { get { return Peek(0); } }
        Token Eat() { Token t = Cur; if (p < toks.Count - 1) p++; return t; }
        bool IsOp(string o) { return Cur.T == Tk.Op && Cur.V == o; }
        bool IsKw(string k) { return Cur.T == Tk.Id && Cur.V == k; }
        void ExpectOp(string o) { if (!IsOp(o)) throw new Exception("第" + Cur.Line + "行: 期望 \"" + o + "\"，实际 \"" + Cur.V + "\""); Eat(); }

        public Script Parse()
        {
            Sc = new Script();
            Sc.Body = ParseStmts(Sc, null);
            return Sc;
        }

        List<Stmt> ParseStmts(Script sc, string until)
        {
            List<Stmt> list = new List<Stmt>();
            while (true)
            {
                if (Cur.T == Tk.Eof) break;
                if (until != null && IsKw(until)) break;
                Stmt s = ParseStmt(sc);
                if (s != null) list.Add(s);
            }
            return list;
        }

        Stmt ParseStmt(Script sc)
        {
            int ln = Cur.Line;
            if (IsKw("子任务"))
            {
                Eat();
                Stmt f = new Stmt(); f.Kind = 6; f.Line = ln;
                f.Name = Eat().V;
                if (IsOp("("))
                {
                    Eat();
                    while (!IsOp(")"))
                    {
                        f.Params.Add(Eat().V);
                        if (IsOp(",")) Eat();
                    }
                    Eat();
                }
                f.Kind = 6;
                f.Bodies.Add(ParseStmts(sc, "子任务完成"));
                if (IsKw("子任务完成")) Eat();
                sc.Funcs[f.Name] = f;
                return f;
            }
            if (IsKw("如果")) return ParseIf(sc);
            if (IsKw("循环"))
            {
                Eat();
                Stmt s = new Stmt(); s.Kind = 2; s.Line = ln;
                s.Conds.Add(ParseExpr());
                s.Bodies.Add(ParseBlock());
                return s;
            }
            if (IsKw("跳出循环")) { Eat(); Stmt s = new Stmt(); s.Kind = 3; s.Line = ln; return s; }
            if (IsKw("继续循环")) { Eat(); Stmt s = new Stmt(); s.Kind = 4; s.Line = ln; return s; }
            if (IsKw("返回"))
            {
                Eat();
                Stmt s = new Stmt(); s.Kind = 5; s.Line = ln;
                if (Cur.T != Tk.Eof && !IsKw("子任务完成") && !IsKw("结束")) s.Expr = ParseExpr();
                return s;
            }
            if (IsKw("变量"))
            {
                Eat();
                string name = Eat().V;
                if (IsOp("="))
                {
                    Eat();
                    ExprNode e = ParseExpr();
                    Stmt s = new Stmt(); s.Kind = 0; s.Line = ln; s.AssignOp = "=";
                    s.Target = new ExprNode(); s.Target.Kind = 1; s.Target.Name = name; s.Target.Line = ln;
                    s.Expr = e;
                    return s;
                }
                Stmt s0 = new Stmt(); s0.Kind = 0; s0.Line = ln; s0.AssignOp = "=";
                s0.Target = new ExprNode(); s0.Target.Kind = 1; s0.Target.Name = name; s0.Target.Line = ln;
                s0.Expr = new ExprNode(); s0.Expr.Kind = 0; s0.Expr.Const = Value.Str("");
                return s0;
            }
            if (IsKw("执行任务文件"))
            {
                Eat();
                Stmt s = new Stmt(); s.Kind = 7; s.Line = ln;
                if (Cur.T == Tk.Str) { s.Expr = new ExprNode(); s.Expr.Kind = 0; s.Expr.Const = Value.Str(Eat().V); }
                else s.Expr = ParseExpr();
                return s;
            }
            // 赋值或表达式语句
            ExprNode e2 = ParseExpr();
            if (IsOp("=") || IsOp("+=") || IsOp("-=") || IsOp("*=") || IsOp("/="))
            {
                string op = Eat().V;
                if (e2.Kind != 1 && e2.Kind != 2) throw new Exception("第" + ln + "行: 不能对该表达式赋值");
                Stmt s = new Stmt(); s.Kind = 0; s.Line = ln; s.AssignOp = op; s.Target = e2; s.Expr = ParseExpr();
                return s;
            }
            Stmt s2 = new Stmt(); s2.Kind = 0; s2.Line = ln; s2.AssignOp = null; s2.Expr = e2;
            return s2;
        }

        Stmt ParseIf(Script sc)
        {
            Eat(); // 如果
            Stmt s = new Stmt(); s.Kind = 1; s.Line = Cur.Line;
            s.Conds.Add(ParseExpr());
            s.Bodies.Add(ParseBlock());
            while (IsKw("否则如果"))
            {
                Eat();
                s.Conds.Add(ParseExpr());
                s.Bodies.Add(ParseBlock());
            }
            if (IsKw("否则"))
            {
                Eat();
                s.ElseBody = ParseBlock();
            }
            return s;
        }

        List<Stmt> ParseBlock()
        {
            if (IsKw("开始")) Eat();
            List<Stmt> b = new List<Stmt>();
            while (!IsKw("结束"))
            {
                if (Cur.T == Tk.Eof) break;
                Stmt s = ParseStmt(Sc);
                if (s != null) b.Add(s);
            }
            if (IsKw("结束")) Eat();
            return b;
        }

        // ---------- 表达式 ----------
        public ExprNode ParseExpr() { return ParseOr(); }
        ExprNode ParseOr()
        {
            ExprNode a = ParseAnd();
            while (IsOp("||"))
            {
                Eat();
                ExprNode b = ParseAnd();
                ExprNode n = new ExprNode(); n.Kind = 3; n.Op = "||"; n.A = a; n.B = b; n.Line = Cur.Line; a = n;
            }
            return a;
        }
        ExprNode ParseAnd()
        {
            ExprNode a = ParseEq();
            while (IsOp("&&"))
            {
                Eat();
                ExprNode b = ParseEq();
                ExprNode n = new ExprNode(); n.Kind = 3; n.Op = "&&"; n.A = a; n.B = b; n.Line = Cur.Line; a = n;
            }
            return a;
        }
        ExprNode ParseEq()
        {
            ExprNode a = ParseRel();
            while (IsOp("==") || IsOp("!="))
            {
                string op = Eat().V;
                ExprNode b = ParseRel();
                ExprNode n = new ExprNode(); n.Kind = 3; n.Op = op; n.A = a; n.B = b; n.Line = Cur.Line; a = n;
            }
            return a;
        }
        ExprNode ParseRel()
        {
            ExprNode a = ParseAdd();
            while (IsOp("<") || IsOp(">") || IsOp("<=") || IsOp(">="))
            {
                string op = Eat().V;
                ExprNode b = ParseAdd();
                ExprNode n = new ExprNode(); n.Kind = 3; n.Op = op; n.A = a; n.B = b; n.Line = Cur.Line; a = n;
            }
            return a;
        }
        ExprNode ParseAdd()
        {
            ExprNode a = ParseMul();
            while (IsOp("+") || IsOp("-"))
            {
                string op = Eat().V;
                ExprNode b = ParseMul();
                ExprNode n = new ExprNode(); n.Kind = 3; n.Op = op; n.A = a; n.B = b; n.Line = Cur.Line; a = n;
            }
            return a;
        }
        ExprNode ParseMul()
        {
            ExprNode a = ParseUnary();
            while (IsOp("*") || IsOp("/") || IsOp("%"))
            {
                string op = Eat().V;
                ExprNode b = ParseUnary();
                ExprNode n = new ExprNode(); n.Kind = 3; n.Op = op; n.A = a; n.B = b; n.Line = Cur.Line; a = n;
            }
            return a;
        }
        ExprNode ParseUnary()
        {
            if (IsOp("-"))
            {
                Eat();
                ExprNode n = new ExprNode(); n.Kind = 4; n.A = ParseUnary(); n.Line = Cur.Line; return n;
            }
            if (IsOp("!"))
            {
                Eat();
                ExprNode n = new ExprNode(); n.Kind = 5; n.A = ParseUnary(); n.Line = Cur.Line; return n;
            }
            return ParsePrimary();
        }
        ExprNode ParsePrimary()
        {
            Token t = Cur;
            if (t.T == Tk.Num)
            {
                Eat();
                ExprNode n = new ExprNode(); n.Kind = 0; n.Line = t.Line;
                if (t.V.IndexOf('.') >= 0) { n.Const = Value.Dbl(double.Parse(t.V, CultureInfo.InvariantCulture)); }
                else n.Const = Value.Int(long.Parse(t.V, CultureInfo.InvariantCulture));
                return n;
            }
            if (t.T == Tk.Str)
            {
                Eat();
                ExprNode n = new ExprNode(); n.Kind = 0; n.Line = t.Line; n.Const = Value.Str(t.V);
                return n;
            }
            if (IsOp("("))
            {
                Eat();
                ExprNode e = ParseExpr();
                ExpectOp(")");
                return e;
            }
            if (t.T == Tk.Id)
            {
                Eat();
                if (IsOp("("))
                {
                    Eat();
                    ExprNode n = new ExprNode(); n.Kind = 6; n.CallName = t.V; n.Line = t.Line; n.Args = new List<ExprNode>();
                    while (!IsOp(")"))
                    {
                        n.Args.Add(ParseExpr());
                        if (IsOp(",")) Eat();
                    }
                    Eat();
                    return n;
                }
                ExprNode v = new ExprNode(); v.Kind = 1; v.Name = t.V; v.Line = t.Line;
                if (IsOp("["))
                {
                    Eat();
                    ExprNode n = new ExprNode(); n.Kind = 2; n.Name = t.V; n.Idx = ParseExpr(); n.Line = t.Line;
                    ExpectOp("]");
                    return n;
                }
                return v;
            }
            throw new Exception("第" + t.Line + "行: 无法识别的标记 \"" + t.V + "\"");
        }
    }

    // ============ 运行环境 ============
    public class Interp
    {
        public Script Sc;
        public Dictionary<string, Slot> Globals = new Dictionary<string, Slot>();
        public Runtime Rt;
        public Interp(Runtime rt) { Rt = rt; }

        public Slot GetSlot(string name)
        {
            lock (Globals)
            {
                Slot s;
                if (!Globals.TryGetValue(name, out s)) { s = new Slot(); Globals[name] = s; }
                return s;
            }
        }

        public static Script Compile(string code)
        {
            Lexer lx = new Lexer(code);
            Parser ps = new Parser(lx.Lex());
            return ps.Parse();
        }

        public void Run(List<Stmt> body)
        {
            RunBlock(body);
        }

        public void RunBlock(List<Stmt> body)
        {
            foreach (Stmt s in body)
            {
                Rt.CheckStop();
                Exec(s);
            }
        }

        public void Exec(Stmt s)
        {
            switch (s.Kind)
            {
                case 0:
                    if (s.AssignOp == null)
                    {
                        Eval(s.Expr);
                        return;
                    }
                    Assign(s);
                    return;
                case 1:
                    for (int i = 0; i < s.Conds.Count; i++)
                    {
                        if (Eval(s.Conds[i]).Truthy()) { RunBlock(s.Bodies[i]); return; }
                    }
                    if (s.ElseBody != null) RunBlock(s.ElseBody);
                    return;
                case 2:
                    while (Eval(s.Conds[0]).Truthy())
                    {
                        Rt.CheckStop();
                        try { RunBlock(s.Bodies[0]); }
                        catch (BreakSignal) { break; }
                        catch (ContinueSignal) { }
                    }
                    return;
                case 3: throw new BreakSignal();
                case 4: throw new ContinueSignal();
                case 5: throw new ReturnSignal(s.Expr == null ? null : Eval(s.Expr));
                case 6: return; // 已提升
                case 7:
                    {
                        string path = Eval(s.Expr).AsStr();
                        string code = System.IO.File.ReadAllText(path, Encoding.UTF8);
                        Script sub = Compile(code);
                        foreach (KeyValuePair<string, Stmt> kv in sub.Funcs) Sc.Funcs[kv.Key] = kv.Value;
                        Run(sub.Body);
                        return;
                    }
            }
        }

        void Assign(Stmt s)
        {
            Value v = Eval(s.Expr);
            if (s.Target.Kind == 6)
            {
                // 形如 a = 累加(100) —— 已在表达式求值中处理过返回值的情况不会走到这里
                Eval(s.Target);
                return;
            }
            if (s.AssignOp != "=")
            {
                Value cur = s.Target.Kind == 1 ? GetSlot(s.Target.Name).V : ReadVar(s.Target.Name, (int)Eval(s.Target.Idx).AsInt());
                Value combined = Binop(s.AssignOp[0].ToString(), cur, v);
                v = combined;
            }
            if (s.Target.Kind == 1)
            {
                Slot sl = GetSlot(s.Target.Name);
                sl.V = v; sl.Arr = null;
            }
            else
            {
                int idx = (int)Eval(s.Target.Idx).AsInt();
                Slot sl = GetSlot(s.Target.Name);
                if (sl.Arr == null) sl.Arr = new List<Value>();
                while (sl.Arr.Count <= idx) sl.Arr.Add(Value.Str(""));
                sl.Arr[idx] = v;
            }
        }

        Value ReadVar(string name, long idx)
        {
            Slot sl = GetSlot(name);
            if (idx < 0) return sl.V;
            if (sl.Arr != null && idx >= 0 && idx < sl.Arr.Count) return sl.Arr[(int)idx];
            return Value.Str("");
        }

        public Value Eval(ExprNode e)
        {
            switch (e.Kind)
            {
                case 0: return e.Const;
                case 1: return GetSlot(e.Name).V;
                case 2: return ReadVar(e.Name, (int)Eval(e.Idx).AsInt());
                case 3:
                    {
                        if (e.Op == "&&") return Eval(e.A).Truthy() && Eval(e.B).Truthy() ? Value.Int(1) : Value.Int(0);
                        if (e.Op == "||") return Eval(e.A).Truthy() || Eval(e.B).Truthy() ? Value.Int(1) : Value.Int(0);
                        Value a = Eval(e.A), b = Eval(e.B);
                        return Binop(e.Op, a, b);
                    }
                case 4: { Value a = Eval(e.A); if (a.T == 1) return Value.Dbl(-a.D); return Value.Int(-a.AsInt()); }
                case 5: return Eval(e.A).Truthy() ? Value.Int(0) : Value.Int(1);
                case 6: return CallBuiltin(e);
            }
            return Value.Str("");
        }

        Value Binop(string op, Value a, Value b)
        {
            if (op == "==") return CmpEq(a, b) ? Value.Int(1) : Value.Int(0);
            if (op == "!=") return !CmpEq(a, b) ? Value.Int(1) : Value.Int(0);
            if (op == "+")
            {
                if (a.T == 2 || b.T == 2) return Value.Str(a.AsStr() + b.AsStr());
                if (a.T == 1 || b.T == 1) return Value.Dbl(a.AsNum() + b.AsNum());
                return Value.Int(a.AsInt() + b.AsInt());
            }
            double x = a.AsNum(), y = b.AsNum();
            switch (op)
            {
                case "-": if (a.T != 1 && b.T != 1) return Value.Int(a.AsInt() - b.AsInt()); return Value.Dbl(x - y);
                case "*": if (a.T != 1 && b.T != 1) return Value.Int(a.AsInt() * b.AsInt()); return Value.Dbl(x * y);
                case "/":
                    if (y == 0) throw new Exception("除数为0");
                    if (a.T != 1 && b.T != 1 && (a.AsInt() % b.AsInt() == 0)) return Value.Int(a.AsInt() / b.AsInt());
                    return Value.Dbl(x / y);
                // 注: 整数整除已优先处理，其余走小数除法
                case "%": if (y == 0) throw new Exception("取模数为0"); return Value.Int(a.AsInt() % b.AsInt());
                case "<": return x < y ? Value.Int(1) : Value.Int(0);
                case ">": return x > y ? Value.Int(1) : Value.Int(0);
                case "<=": return x <= y ? Value.Int(1) : Value.Int(0);
                case ">=": return x >= y ? Value.Int(1) : Value.Int(0);
            }
            throw new Exception("未知运算符 " + op);
        }
        bool CmpEq(Value a, Value b)
        {
            if (a.T == 2 || b.T == 2) return a.AsStr() == b.AsStr();
            if (a.T == 1 || b.T == 1) return a.AsNum() == b.AsNum();
            return a.AsInt() == b.AsInt();
        }

        // 内置调用。参数中的裸标识符作为“引用参数”传给内置函数
        Value CallBuiltin(ExprNode e)
        {
            // 子任务调用
            Stmt f;
            if (Sc.Funcs.TryGetValue(e.CallName, out f))
            {
                Rt.CheckStop();
                List<Slot> saved = new List<Slot>();
                Dictionary<string, Slot> patch = new Dictionary<string, Slot>();
                for (int i = 0; i < f.Params.Count; i++)
                {
                    string pname = f.Params[i];
                    Slot target;
                    if (i < e.Args.Count && e.Args[i].Kind == 1)
                        target = GetSlot(e.Args[i].Name);   // 引用别名
                    else if (i < e.Args.Count && e.Args[i].Kind == 2)
                    {
                        Value v = Eval(e.Args[i]);
                        target = new Slot(); target.V = v;
                    }
                    else
                    {
                        Value v = i < e.Args.Count ? Eval(e.Args[i]) : Value.Str("");
                        target = new Slot(); target.V = v;
                    }
                    patch[pname] = target;
                }
                // 替换参数名槽位（保存原槽位，调用后恢复）
                Dictionary<string, Slot> orig = new Dictionary<string, Slot>();
                lock (Globals)
                {
                    foreach (KeyValuePair<string, Slot> kv in patch)
                    {
                        Slot o;
                        Globals.TryGetValue(kv.Key, out o);
                        orig[kv.Key] = o;
                        Globals[kv.Key] = kv.Value;
                    }
                }
                try
                {
                    RunBlock(f.Bodies[0]);
                    return Value.Int(0);
                }
                catch (ReturnSignal r) { return r.V == null ? Value.Int(0) : r.V; }
                finally
                {
                    lock (Globals)
                    {
                        foreach (KeyValuePair<string, Slot> kv in orig)
                        {
                            if (kv.Value == null) Globals.Remove(kv.Key); else Globals[kv.Key] = kv.Value;
                        }
                    }
                }
            }
            // 内置函数
            Rt.CheckStop();
            CallArgs args = new CallArgs();
            foreach (ExprNode a in e.Args)
            {
                if (a.Kind == 1) args.AddRef(GetSlot(a.Name), -1);
                else if (a.Kind == 2)
                {
                    int idx = (int)Eval(a.Idx).AsInt();
                    args.AddRef(GetSlot(a.Name), idx);
                }
                else args.AddVal(Eval(a));
            }
            Value ret = Rt.Call(e.CallName, args);
            return ret;
        }
    }

    // 调用参数（值或引用）
    public class CallArgs
    {
        public List<object> Items = new List<object>(); // Value 或 RefArg
        public void AddVal(Value v) { Items.Add(v); }
        public void AddRef(Slot s, int idx) { Items.Add(new RefArg { Slot = s, Index = idx }); }
        public int Count { get { return Items.Count; } }
        public Value V(int i)
        {
            if (i >= Items.Count) return Value.Int(0);
            object o = Items[i];
            if (o is Value) return (Value)o;
            return ((RefArg)o).Get();
        }
        public RefArg R(int i)
        {
            if (i >= Items.Count) return null;
            return Items[i] as RefArg;
        }
        public int I(int i) { return (int)V(i).AsInt(); }
        public double D(int i) { return V(i).AsDbl(); }
        public string S(int i) { return V(i).AsStr(); }
        public void SetRef(int i, Value v)
        {
            RefArg r = R(i);
            if (r != null) r.Set(v);
        }
    }
}
