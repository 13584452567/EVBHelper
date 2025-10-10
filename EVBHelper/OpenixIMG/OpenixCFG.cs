using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace OpenixIMG
{
    public class Variable
    {
        public enum ValueType { Number, String, Reference, ListItem, Unknown }

        public string Name { get; }
        public ValueType Type { get; }
        private readonly object _value;

        public Variable(string name, ValueType type, object value)
        {
            Name = name;
            Type = type;
            _value = value;
        }
        
        public Variable(string name, object value, ValueType type) : this(name, type, value)
        {
        }

        public void AddItem(Variable item)
        {
            if (Type == ValueType.ListItem && _value is List<Variable> list)
            {
                list.Add(item);
            }
        }

        public List<Variable> GetItems() => Type == ValueType.ListItem ? (List<Variable>)_value : new List<Variable>();

        public uint GetValueAsUInt() => _value is uint u ? u : 0;
        public string GetValueAsString() => _value.ToString() ?? string.Empty;
    }

    public class Group
    {
        public string Name { get; }
        public List<Variable> Variables { get; } = new List<Variable>();

        public Group(string name)
        {
            Name = name;
        }

        public void AddVariable(Variable variable)
        {
            Variables.Add(variable);
        }
    }

    public class Config
    {
        private readonly List<Group> _groups = new List<Group>();
        private readonly Dictionary<string, Group> _groupMap = new Dictionary<string, Group>();
        private readonly Dictionary<string, Variable> _variableMap = new Dictionary<string, Variable>();
        private readonly bool _verbose;

        public Config(bool verbose = false)
        {
            _verbose = verbose;
        }

        public bool Parse(string data)
        {
            using (var stream = new StringReader(data))
            {
                string? line;
                Group? currentGroup = null;

                while ((line = stream.ReadLine()) != null)
                {
                    line = line.Trim();
                    if (string.IsNullOrEmpty(line) || line.StartsWith("#") || line.StartsWith(";"))
                    {
                        continue;
                    }

                    if (line.StartsWith("[") && line.EndsWith("]"))
                    {
                        string groupName = line.Substring(1, line.Length - 2);
                        currentGroup = new Group(groupName);
                        _groups.Add(currentGroup);
                        _groupMap[groupName] = currentGroup;
                    }
                    else if (currentGroup != null && line.Contains('='))
                    {
                        var parts = line.Split(new[] { '=' }, 2);
                        string varName = parts[0].Trim();
                        string varValueStr = parts[1].Trim();

                        object varValue;
                        Variable.ValueType type;

                        if (varValueStr.StartsWith("\"") && varValueStr.EndsWith("\""))
                        {
                            varValue = varValueStr.Substring(1, varValueStr.Length - 2);
                            type = Variable.ValueType.String;
                        }
                        else if (uint.TryParse(varValueStr, out uint numValue))
                        {
                            varValue = numValue;
                            type = Variable.ValueType.Number;
                        }
                        else
                        {
                            varValue = varValueStr;
                            type = Variable.ValueType.Reference;
                        }

                        var variable = new Variable(varName, type, varValue);
                        currentGroup.AddVariable(variable);
                        _variableMap[varName] = variable;
                    }
                }
            }
            return true;
        }

        public Group? FindGroup(string name) => _groupMap.TryGetValue(name, out var group) ? group : null;

        public Variable? FindVariable(string name) => _variableMap.TryGetValue(name, out var variable) ? variable : null;

        public void PrintConfig()
        {
            foreach (var group in _groups)
            {
                Console.WriteLine($"[{group.Name}]");
                foreach (var variable in group.Variables)
                {
                    Console.WriteLine($"  {variable.Name} = {variable.GetValueAsString()}");
                }
            }
        }

        public Variable? FindVariable(Group group, string name)
        {
            return group?.Variables.FirstOrDefault(v => v.Name == name);
        }

        public void AddGroup(Group group)
        {
            _groups.Add(group);
            _groupMap[group.Name] = group;
        }

        public void AddVariable(Group group, Variable variable)
        {
            group.AddVariable(variable);
        }

        private Variable? ParseExpression(string expr)
        {
            if (string.IsNullOrEmpty(expr)) return null;

            var parts = expr.Split('.');
            if (parts.Length != 2) return null;

            var group = FindGroup(parts[0]);
            if (group == null) return null;

            return FindVariable(group, parts[1]);
        }

        private Variable? ParseTerm(string term)
        {
            if (string.IsNullOrEmpty(term)) return null;

            if (term.StartsWith("\"") && term.EndsWith("\""))
            {
                return new Variable("", Variable.ValueType.String, term.Substring(1, term.Length - 2));
            }
            if (uint.TryParse(term, out uint val))
            {
                return new Variable("", Variable.ValueType.Number, val);
            }
            return ParseExpression(term);
        }

        private Variable? ParseOperation(string op, Variable left, Variable right)
        {
            if (left.Type == Variable.ValueType.Number && right.Type == Variable.ValueType.Number)
            {
                uint leftVal = left.GetValueAsUInt();
                uint rightVal = right.GetValueAsUInt();
                return op switch
                {
                    "+" => new Variable("", Variable.ValueType.Number, leftVal + rightVal),
                    "-" => new Variable("", Variable.ValueType.Number, leftVal - rightVal),
                    "*" => new Variable("", Variable.ValueType.Number, leftVal * rightVal),
                    "/" => new Variable("", Variable.ValueType.Number, leftVal / rightVal),
                    _ => null,
                };
            }
            return null;
        }
    }
}
