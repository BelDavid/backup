using System;
using System.Collections;
using System.Collections.Specialized;
using System.Reflection;
using System.Text.RegularExpressions;

namespace Backup
{
    public sealed class Parameters: IEnumerable<Parameter>
    {
        private readonly Parameter[] parameters;
        private readonly Dictionary<string, Parameter> parameterMap;
        private readonly Dictionary<char, Parameter> parameterFlagMap;

        private readonly Regex regexLongNameValue = new ("^--(.+)=(.+)$");
        private readonly Regex regexSwitchOrName = new ("^--([^=]+)$");
        private readonly Regex regexFlags = new("^-([^-].*)$");
        private readonly Regex regexPlainValue = new ("^[^-=][^=]*$");

        public readonly Parameter paramHelp = new SwitchParameter('h', "help", "Print detailed info about parameters.");

        public readonly string skipParamValue = "-";

        /// <summary>
        /// 
        /// It's advised to put SwitchParameters at the end, since they are not treated as positional parameters
        /// </summary>
        /// <param name="parameters"></param>
        /// <exception cref="NullReferenceException">All passed parameters must not be null</exception>
        /// <exception cref="ArgumentException">Every parameter.Name must be unique</exception>
        public Parameters(params Parameter[] parameters)
        {
            this.parameters = parameters ?? throw new NullReferenceException("[Params] Can not have a null parameter definition."); ;
            parameterMap = new Dictionary<string, Parameter>();
            parameterFlagMap = new Dictionary<char, Parameter>();

            foreach (var param in parameters)
            {
                if (param is null){
                    throw new NullReferenceException("[Params] Can not have a null parameter definition.");
                }
                if (param.LongName == paramHelp.LongNameLowered || param.Flag == paramHelp.Flag)
                {
                    throw new ArgumentException($"[Params] Can not override help parameter!");
                }
                if (parameterMap.ContainsKey(param.LongNameLowered))
                {
                    throw new ArgumentException($"[Params] Can not have more than one parameter with the same name: {param.LongName}");
                }
                if (parameterFlagMap.ContainsKey(param.Flag))
                {
                    throw new ArgumentException($"[Params] Can not have more than one parameter with the same flag: {param.Flag}");
                }
                parameterMap.Add(param.LongNameLowered, param);
                parameterFlagMap.Add(param.Flag, param);
            }
        }

        public ParamsState Evaluate(string[] args)
        {
            bool valid = true;

            // Look for Help argument
            foreach (var arg in args)
            {
                if (arg.ToLower() == "--"+paramHelp.LongNameLowered || arg == "-"+paramHelp.Flag)
                {
                    paramHelp.SetValue(true);
                    PrettyPrint.WriteLine("Parameters: ", OutputType.Help);
                    foreach (var param in parameters.Append(paramHelp))
                    {
                        PrettyPrint.Write($" -{param.Flag}, --{param.LongName}", OutputType.Help);
                        if (param.HasDefaultValue)
                        {
                            PrettyPrint.Write(" = " + param.GetValueAsObject(), OutputType.Help, ConsoleColor.DarkYellow);
                        }
                        if (param.Mandatory)
                        {
                            PrettyPrint.Write(" | Required", OutputType.Help, ConsoleColor.DarkYellow);
                        }
                        PrettyPrint.WriteLine();
                        PrettyPrint.WriteLine($"   # {param.Description}", OutputType.Help, ConsoleColor.Gray);
                    }

                    return ParamsState.InvalidClean;
                }
            }

            if (args.Length == 1 && args[0] == "list_parameters_plain")
            {
                foreach (var param in parameters.Append(paramHelp))
                {
                    PrettyPrint.WriteLine($"--{param.LongName}{(param is SwitchParameter ? "": "=")}", OutputType.Help, ConsoleColor.White);
                }
                return ParamsState.InvalidClean;
            }

            // --- Parse ---
            var parametersEnumerator = EvalGetParametersEnumerator();
            Parameter? previousParamDef = null;
            foreach (var arg in args)
            {
                var paramAvailable = parametersEnumerator.MoveNext();
                var paramToSet = parametersEnumerator.Current;
                var argValue = arg;

                if (previousParamDef == null)
                {
                    // Not continuing previous cycle
                    var matchLongNameValue = regexLongNameValue.Match(arg);
                    var matchSwitchOrName = regexSwitchOrName.Match(arg);
                    var matchFlags = regexFlags.Match(arg);

                    if (matchLongNameValue.Success)
                    {
                        // Argument provides parameter name and value
                        var paramName = matchLongNameValue.Groups[1].Value.ToLower();
                        argValue = matchLongNameValue.Groups[2].Value;
                        if (!parameterMap.ContainsKey(paramName))
                        {
                            PrettyPrint.WriteLine($"[Params] No parameter named '{paramName}' found!", OutputType.Error);
                            valid = false;
                            continue;
                        }

                        paramToSet = parameterMap[paramName];

                        if (paramToSet is SwitchParameter switchParameter)
                        {
                            PrettyPrint.WriteLine("[Params] Can not set value of a SwitchParameter!", OutputType.Error);
                            valid = false;
                            continue;
                        }
                        if (paramToSet.IsSet)
                        {
                            PrettyPrint.WriteLine($"[Params] Parameter '{paramName}' is already set!", OutputType.Error);
                            valid = false;
                            continue;
                        }
                    }
                    else if (matchSwitchOrName.Success)
                    {
                        // Argument provides parameter name
                        var paramName = matchSwitchOrName.Groups[1].Value.ToLower();
                        if (!parameterMap.ContainsKey(paramName))
                        {
                            PrettyPrint.WriteLine($"[Params] No parameter named '{paramName}'!", OutputType.Error);
                            valid = false;
                            continue;
                        }

                        paramToSet = parameterMap[paramName];
                        if (paramToSet is SwitchParameter switchParam)
                        {
                            // Switch parameter does not pass value
                            switchParam.SetValue(true);
                        }
                        else
                        {
                            // Argument value is expected in the next cycle
                            previousParamDef = paramToSet;
                        }
                        continue;
                    }
                    else if (matchFlags.Success)
                    {
                        // Process flags
                        var flags = matchFlags.Groups[1].ValueSpan;
                        bool continueBigCycle = true;
                        for (int i = 0; i < flags.Length; i++)
                        {
                            char flag = flags[i];
                            if (!parameterFlagMap.ContainsKey(flag))
                            {
                                PrettyPrint.WriteLine($"[Params] No parameter with flag '{flag}'!", OutputType.Error);
                                valid = false;
                                break;
                            }
                            var param = parameterFlagMap[flag];
                            if (param is SwitchParameter sp)
                            {
                                sp.SetValue(true);
                            } 
                            else
                            {
                                var value = flags[(i + 1)..];
                                if (value.Length > 0)
                                {
                                    argValue = value.ToString();
                                    paramToSet = param;
                                    continueBigCycle = false;
                                    break;
                                }
                                else
                                {
                                    previousParamDef = param;
                                }
                            }
                        }
                        if (continueBigCycle)
                        { 
                            continue; 
                        }
                    }
                    else
                    {
                        // Positional parameter
                        if (!paramAvailable)
                        {
                            PrettyPrint.WriteLine($"[Params] No available parameter to assign value '{arg}'!", OutputType.Error);
                            valid = false;
                            continue;
                        }

                        if (argValue == skipParamValue)
                        {
                            // Don't set the parameter value, but skip it's processing
                            skipPositionalParameter = true;
                            continue;
                        }
                    }
                }
                else
                {
                    // Continuing with name defined in previuos cycle, arg must be plain value
                    paramToSet = previousParamDef;
                    previousParamDef = null;
                    
                    var matchPlainValue = regexPlainValue.Match(arg);
                    if (!matchPlainValue.Success)
                    {
                        PrettyPrint.WriteLine($"[Params] Missing value for parameter '{paramToSet.LongName}'!", OutputType.Error);
                        valid = false;
                        continue;
                    }
                }

                //
                if (paramToSet is null)
                {
                    PrettyPrint.WriteLine($"[Params] Unknown argument '{arg}'!", OutputType.Error);
                    valid = false;
                    continue;
                }

                if (previousParamDef != null)
                {
                    PrettyPrint.WriteLine($"[Params] Missing value for parameter '{previousParamDef.LongName}'!", OutputType.Error);
                }

                object? parsedValue = null;
                // TryParse argument value and assign it to paramToSet
                try
                {
                    parsedValue = Convert.ChangeType(argValue, paramToSet.GetValueType());
                }
                catch (Exception)
                {
                    PrettyPrint.WriteLine($"[Params] Argument value '{argValue}' for parameter '{paramToSet.LongName}' could not be converted to '{paramToSet.GetValueType().Name}'!", OutputType.Error);
                    valid = false;
                }

                // Assign parsed value
                if (!paramToSet.SetValue(parsedValue))
                {
                    PrettyPrint.WriteLine($"[Params]  -{paramToSet.Flag}, --{paramToSet.LongName}: {paramToSet.Description}", OutputType.Error, ConsoleColor.Yellow);
                    valid = false;
                }
            }

            if (previousParamDef != null)
            {
                PrettyPrint.WriteLine($"[Params] Missing value for parameter '{previousParamDef.LongName}'!", OutputType.Error);
                valid = false;
            }

            // All mandatory parameters should be set
            foreach (var param in parameters)
            {
                if (param.Mandatory && !param.IsSet)
                {
                    PrettyPrint.WriteLine($"[Params] Parameter {param.LongName} is required!", OutputType.Error, ConsoleColor.Yellow);
                    PrettyPrint.WriteLine($"[Params]  -{param.Flag}, --{param.LongName}: {param.Description}", OutputType.Error, ConsoleColor.Yellow);
                    valid = false;
                }
            }

            if (!valid)
            {
                PrettyPrint.WriteLine("[Params] Use -h, or --help to list all parameters.", OutputType.Help);
            }

            return valid ? ParamsState.Correct : ParamsState.InvalidError;
        }

        /// <summary>
        /// When set to true, EvalGetParametersEnumerator will move to the next positional parameter regardless whether the current parameter is set
        /// </summary>
        private bool skipPositionalParameter = false;

        /// <summary>
        /// Enumerates through unset non-switch parameters and keeps returning one parameter until it's set or skipped
        /// </summary>
        /// <returns></returns>
        private IEnumerator<Parameter> EvalGetParametersEnumerator()
        {
            foreach (var param in parameters)
            {
                while (!param.IsSet && param.GetType() != typeof(SwitchParameter))
                {
                    if (skipPositionalParameter)
                    {
                        skipPositionalParameter = false;
                        break;
                    }
                    yield return param;
                }
            }
        }

        public IEnumerator<Parameter> GetEnumerator() => ((IEnumerable<Parameter>)parameters).GetEnumerator();
        IEnumerator IEnumerable.GetEnumerator() => parameters.GetEnumerator();

        public object? this[string longName] => parameterMap[longName].GetValueAsObject();


    }

    public abstract class Parameter
    {
        public char Flag { get; init; }
        public string LongName { get; init; }
        public string LongNameLowered { get; init; }
        public string Description { get; init; }
        public bool Mandatory { get; init; }

        public bool IsSet { get; protected set; }
        public bool HasDefaultValue { get; init; }

        public Parameter(char flag, string longName, string description, bool mandatory)
        {
            // Ensure flag is alphabetic symbol
            if (!(flag >= 'a' &&  flag <= 'z' || flag >= 'A' && flag <= 'Z'))
            {
                throw new ArgumentException($"Flag must be alphabetic symbol a-zA-Z, not '{flag}'");
            }
            this.Flag = flag;
            this.LongName = longName;
            this.LongNameLowered = longName.ToLower();
            this.Description = description;
            this.Mandatory = mandatory;
        }

        public abstract Type GetValueType();

        public abstract bool SetValue(object? value);
        public abstract object? GetValueAsObject();
        public abstract void Clear();
    }

    public sealed class Parameter<T> : Parameter
    {
        private readonly static Func<T, bool> alwaysTrueConstrain = _ => true;

        public T? Value { get; private set; }
        public readonly Func<T, bool> constrain;

        public Parameter(char flag, string longName, string description, bool mandatory = false, Func<T, bool>? constrain = null, T? defaultValue = default): base(flag, longName, description, mandatory)
        {
            Value = defaultValue;
            HasDefaultValue = defaultValue != null;
            this.constrain = constrain ?? alwaysTrueConstrain;
        }

        public override Type GetValueType() => typeof(T);

        public override bool SetValue(object? value) => value is T val && SetValue(val);
        public bool SetValue(T value)
        {
            if (constrain(value))
            {
                Value = value;
                IsSet = true;
                return true;
            }
            PrettyPrint.WriteLine($"[Params] Invalid value '{value}' for parameter '{LongName}', constrain check failed.", OutputType.Error);
            return false;
        }
        public override object? GetValueAsObject() => Value;

        public static implicit operator T?(Parameter<T> param) => param.Value;
        public override void Clear()
        {
            Value = default;
            IsSet = false;
        }
    }

    public sealed class SwitchParameter : Parameter
    {
        public SwitchParameter(char flag, string longName, string description) : base(flag, longName, description, false) { }

        public override object? GetValueAsObject() => IsSet;
        public bool GetValue() => IsSet;

        public override Type GetValueType() => typeof(bool);

        public override bool SetValue(object? value) => value is bool val && SetValue(val);
        public bool SetValue(bool value)
        {
            IsSet = value;
            return true;
        }
        public override void Clear()
        {
            IsSet = false;
        }
    }

    /// <summary>
    /// State of the parameters after running Parameters.Evaluate() method.
    /// Bit #0: Correctness. 1=Correct
    /// Bit #1: Cleanliness. 1=Clean
    /// </summary>
    public enum ParamsState
    {
        /// <summary>
        /// Params are set correctly
        /// </summary>
        Correct = 0b01,

        /// <summary>
        /// Params are not correct, but special case has been handled. Exit clean without error.
        /// </summary>
        InvalidClean = 0b10,

        /// <summary>
        /// Parameters were not set correctly, anounce error.
        /// </summary>
        InvalidError = 0b00
    }
}
