using System;
using System.Linq;
using HotChocolate;
using HotChocolate.Configuration;
using HotChocolate.Internal;
using HotChocolate.Types;
using HotChocolate.Types.Descriptors;
using HotChocolate.Types.Descriptors.Definitions;
using HotChocolate.Types.Relay;
using Microsoft.Extensions.DependencyInjection;

namespace AutoGuru.HotChocolate.Types.Relay
{
    internal sealed class PolymorphicIdsTypeInterceptor : TypeInterceptor
    {
        private const string StandardGlobalIdFormatterName = "GlobalIdInputValueFormatter";

        // /src/HotChocolate/Core/src/Abstractions/WellKnownContextData.cs
        private const string GlobalIdSupportEnabledContextDataKey = "HotChocolate.Relay.GlobalId";

        private PolymorphicIdsOptions? _options;
        private PolymorphicIdsOptions Options =>
            _options ?? throw new Exception("Options weren't set up");

        public override void OnBeforeCompleteType(
            ITypeCompletionContext completionContext,
            DefinitionBase definition)
        {
            var globalContextData = completionContext.ContextData;
            if (!globalContextData.ContainsKey(GlobalIdSupportEnabledContextDataKey))
            {
                var error = SchemaErrorBuilder.New()
                    .SetMessage(
                        "Global ID support isn't enabled but is required for " +
                        "AutoGuru.HotChocolate.PolymorphicIds. Please ensure that " +
                        $"{nameof(RelaySchemaBuilderExtensions.AddGlobalObjectIdentification)} " +
                        "is called during startup.")
                    .Build();
                throw new SchemaException(error);
            }

            if (completionContext.ContextData.TryGetValue(
                    typeof(PolymorphicIdsOptions).FullName!,
                    out var o) &&
                o is PolymorphicIdsOptions options)
            {
                _options = options;
            }

            if (definition is InputObjectTypeDefinition inputObjectTypeDefinition)
            {
                foreach (var inputFieldDefinition in inputObjectTypeDefinition.Fields)
                {
                    var idInfo = GetIdInfo(completionContext, inputFieldDefinition);
                    if (idInfo != null && ShouldIntercept(idInfo.Value.IdRuntimeType))
                    {
                        DeferFormatterReplacement(
                            inputFieldDefinition,
                            idInfo.Value.NodeTypeName,
                            idInfo.Value.IdRuntimeType);
                    }
                }
            }
            else if (definition is ObjectTypeDefinition objectTypeDefinition)
            {
                var isQueryType = definition.Name == OperationTypeNames.Query;

                foreach (var objectFieldDefinition in objectTypeDefinition.Fields)
                {
                    if (isQueryType && objectFieldDefinition.Name == "node")
                    {
                        continue;
                    }

                    foreach (var argumentDefinition in objectFieldDefinition.Arguments)
                    {
                        var idInfo = GetIdInfo(completionContext, argumentDefinition);
                        if (idInfo != null && ShouldIntercept(idInfo.Value.IdRuntimeType))
                        {
                            DeferFormatterReplacement(
                                argumentDefinition,
                                idInfo.Value.NodeTypeName,
                                idInfo.Value.IdRuntimeType);
                        }
                    }
                }
            }

            base.OnBeforeCompleteType(completionContext, definition);
        }

        // Hot Chocolate adds its own GlobalIdInputValueFormatter during the BeforeCompletion
        // configuration phase, which runs *after* OnBeforeCompleteType. So we register our own
        // BeforeCompletion configuration (appended after Hot Chocolate's) that runs in the same
        // phase, by which time the default formatter is present and can be replaced.
        private static void DeferFormatterReplacement(
            ArgumentDefinition argumentDefinition,
            string typeName,
            Type idRuntimeType)
        {
            argumentDefinition.Configurations.Add(
                new CompleteConfiguration(
                    (completionContext, _) => InsertFormatter(
                        completionContext,
                        argumentDefinition,
                        typeName,
                        idRuntimeType),
                    argumentDefinition,
                    ApplyConfigurationOn.BeforeCompletion));
        }

        private static void InsertFormatter(
            ITypeCompletionContext completionContext,
            ArgumentDefinition argumentDefinition,
            string typeName,
            Type idRuntimeType)
        {
            var formatter = new PolymorphicIdInputValueFormatter(
                typeName,
                idRuntimeType,
                completionContext.DescriptorContext.NodeIdSerializerAccessor);

            var formatters = argumentDefinition.Formatters;
            var defaultFormatter = formatters
                .FirstOrDefault(f => f.GetType().Name == StandardGlobalIdFormatterName);

            if (defaultFormatter is null)
            {
                formatters.Insert(0, formatter);
            }
            else
            {
                // Replace Hot Chocolate's GlobalIdInputValueFormatter rather than running
                // before it. As of HC14 formatters are chained, and the built-in formatter
                // throws on the raw (database) id values we intentionally produce, so it must
                // not run after ours.
                formatters[formatters.IndexOf(defaultFormatter)] = formatter;
            }
        }

        private bool ShouldIntercept(Type idRuntimeType)
        {
            if (!Options.HandleGuidIds &&
                (idRuntimeType == typeof(Guid) || idRuntimeType == typeof(Guid?)))
            {
                return false;
            }

            if (!Options.HandleIntIds &&
                (idRuntimeType == typeof(int) || idRuntimeType == typeof(int?)))
            {
                return false;
            }

            if (!Options.HandleLongIds &&
                (idRuntimeType == typeof(long) || idRuntimeType == typeof(long?)))
            {
                return false;
            }

            if (!Options.HandleStringIds &&
                idRuntimeType == typeof(string))
            {
                return false;
            }

            return true;
        }

        private static (string NodeTypeName, Type IdRuntimeType)? GetIdInfo(
            ITypeCompletionContext completionContext,
            ArgumentDefinition definition)
        {
            var typeInspector = completionContext.TypeInspector;
            IDAttribute? idAttribute = null;
            IExtendedType? idType = null;

            if (definition is InputFieldDefinition inputField)
            {
                // UseSorting arg/s seems to come in here with a null Property
                if (inputField.Property == null)
                {
                    return null;
                }

                idAttribute = (IDAttribute?)inputField.Property
                   .GetCustomAttributes(inherit: true)
                   .SingleOrDefault(a => a is IDAttribute);
                if (idAttribute == null)
                {
                    return null;
                }

                idType = typeInspector.GetReturnType(inputField.Property, true);
            }
            else if (definition.Parameter is not null)
            {
                idAttribute = (IDAttribute?)definition.Parameter
                    .GetCustomAttributes(inherit: true)
                    .SingleOrDefault(a => a is IDAttribute);
                if (idAttribute == null)
                {
                    return null;
                }

                idType = typeInspector.GetArgumentType(definition.Parameter, true);
            }
            else if (definition.Type is ExtendedTypeReference typeReference)
            {
                if (typeReference.Type.Kind == ExtendedTypeKind.Schema)
                {
                    return null;
                }
            }
            else if (definition.Type is SyntaxTypeReference syntaxTypeReference)
            {
                return null;
            }

            if (idAttribute is null || idType is null)
            {
                throw new SchemaException(SchemaErrorBuilder.New()
                    .SetMessage("Unable to resolve type from field `{0}`.", definition.Name)
                    .SetTypeSystemObject(completionContext.Type)
                    .Build());
            }

            var idRuntimeType = idType.ElementType?.Source ?? idType.Source;
            var nodeTypeName = idAttribute?.TypeName != null
                ? idAttribute.TypeName
                : completionContext.Type.Name;

            return (nodeTypeName, idRuntimeType);
        }
    }
}
