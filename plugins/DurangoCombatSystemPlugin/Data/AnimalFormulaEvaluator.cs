using System;
using System.Globalization;

namespace Baominix.DurangoOriginal.CombatSystem.Data
{
    internal static class AnimalFormulaEvaluator
    {
        internal static float Evaluate(
            string expression,
            int combatLevel,
            float unstableFactor,
            float fallback,
            out bool exact)
        {
            exact = false;
            if (string.IsNullOrEmpty(expression))
            {
                return fallback;
            }

            try
            {
                Parser parser = new Parser(
                    expression,
                    Math.Max(1, combatLevel),
                    Math.Max(0.01f, unstableFactor));
                float value = parser.Evaluate();
                if (float.IsNaN(value) || float.IsInfinity(value))
                {
                    return fallback;
                }
                exact = true;
                return value;
            }
            catch
            {
                return fallback;
            }
        }

        private sealed class Parser
        {
            private readonly string _text;
            private readonly float _combatLevel;
            private readonly float _unstableFactor;
            private int _index;

            internal Parser(
                string text,
                float combatLevel,
                float unstableFactor)
            {
                _text = text ?? string.Empty;
                _combatLevel = combatLevel;
                _unstableFactor = unstableFactor;
            }

            internal float Evaluate()
            {
                float value = ParseExpression();
                SkipSpaces();
                if (_index != _text.Length)
                {
                    throw new FormatException();
                }
                return value;
            }

            private float ParseExpression()
            {
                float value = ParseTerm();
                while (true)
                {
                    SkipSpaces();
                    if (Take('+'))
                    {
                        value += ParseTerm();
                    }
                    else if (Take('-'))
                    {
                        value -= ParseTerm();
                    }
                    else
                    {
                        return value;
                    }
                }
            }

            private float ParseTerm()
            {
                float value = ParsePower();
                while (true)
                {
                    SkipSpaces();
                    if (Take('*'))
                    {
                        value *= ParsePower();
                    }
                    else if (Take('/'))
                    {
                        float divisor = ParsePower();
                        if (Math.Abs(divisor) <= float.Epsilon)
                        {
                            throw new DivideByZeroException();
                        }
                        value /= divisor;
                    }
                    else
                    {
                        return value;
                    }
                }
            }

            private float ParsePower()
            {
                float value = ParseFactor();
                SkipSpaces();
                if (_index + 1 < _text.Length &&
                    _text[_index] == '*' &&
                    _text[_index + 1] == '*')
                {
                    _index += 2;
                    value = (float)Math.Pow(value, ParsePower());
                }
                return value;
            }

            private float ParseFactor()
            {
                SkipSpaces();
                if (Take('+'))
                {
                    return ParseFactor();
                }
                if (Take('-'))
                {
                    return -ParseFactor();
                }
                if (Take('('))
                {
                    float value = ParseExpression();
                    SkipSpaces();
                    if (!Take(')'))
                    {
                        throw new FormatException();
                    }
                    return value;
                }

                if (_index < _text.Length &&
                    (char.IsLetter(_text[_index]) ||
                     _text[_index] == '_'))
                {
                    string identifier = ParseIdentifier();
                    if (string.Equals(
                        identifier,
                        "combat_level",
                        StringComparison.Ordinal))
                    {
                        return _combatLevel;
                    }
                    if (string.Equals(
                        identifier,
                        "unstable_factor",
                        StringComparison.Ordinal))
                    {
                        return _unstableFactor;
                    }
                    throw new FormatException();
                }

                int start = _index;
                bool hasExponent = false;
                while (_index < _text.Length)
                {
                    char character = _text[_index];
                    if (char.IsDigit(character) || character == '.')
                    {
                        _index++;
                        continue;
                    }
                    if ((character == 'e' || character == 'E') &&
                        !hasExponent)
                    {
                        hasExponent = true;
                        _index++;
                        if (_index < _text.Length &&
                            (_text[_index] == '+' ||
                             _text[_index] == '-'))
                        {
                            _index++;
                        }
                        continue;
                    }
                    break;
                }
                if (start == _index)
                {
                    throw new FormatException();
                }
                return float.Parse(
                    _text.Substring(start, _index - start),
                    CultureInfo.InvariantCulture);
            }

            private string ParseIdentifier()
            {
                int start = _index;
                while (_index < _text.Length)
                {
                    char character = _text[_index];
                    if (!char.IsLetterOrDigit(character) &&
                        character != '_')
                    {
                        break;
                    }
                    _index++;
                }
                return _text.Substring(start, _index - start);
            }

            private bool Take(char expected)
            {
                if (_index < _text.Length &&
                    _text[_index] == expected)
                {
                    _index++;
                    return true;
                }
                return false;
            }

            private void SkipSpaces()
            {
                while (_index < _text.Length &&
                    char.IsWhiteSpace(_text[_index]))
                {
                    _index++;
                }
            }
        }
    }
}
