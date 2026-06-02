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
                    if (idInfo is { } info && ShouldIntercept(info.RuntimeType))
                    {
                        DeferFormatterReplacement(
                            inputFieldDefinition, info.RuntimeType, info.ExpectedTypeName);
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
                        if (idInfo is { } info && ShouldIntercept(info.RuntimeType))
                        {
                            DeferFormatterReplacement(
                                argumentDefinition, info.RuntimeType, info.ExpectedTypeName);
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
            Type idRuntimeType,
            string? expectedTypeName)
        {
            argumentDefinition.Configurations.Add(
                new CompleteConfiguration(
                    (completionContext, _) => InsertFormatter(
                        completionContext,
                        argumentDefinition,
                        idRuntimeType,
                        expectedTypeName),
                    argumentDefinition,
                    ApplyConfigurationOn.BeforeCompletion));
        }

        private static void InsertFormatter(
            ITypeCompletionContext completionContext,
            ArgumentDefinition argumentDefinition,
            Type idRuntimeType,
            string? expectedTypeName)
        {
            var formatter = new PolymorphicIdInputValueFormatter(
                idRuntimeType,
                expectedTypeName,
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

        // Resolves the CLR runtime type of an id field/argument and the node type name it must
        // validate against, or null if it isn't a relay ID we should handle. A field is treated
        // as a relay ID if it carries the [ID] attribute (attribute style) or its GraphQL type
        // was rewritten to IdType - both the attribute and the fluent `.ID()` declaration do
        // this before completion, which is how we support fluent-style ids (see issue #5).
        //
        // ExpectedTypeName is only set when an explicit type name was declared (e.g.
        // `[ID("Booking")]`), mirroring Hot Chocolate's own type-name validation; it's null for
        // a bare `[ID]` (and for fluent ids, whose explicit name isn't reachable here).
        private static (Type RuntimeType, string? ExpectedTypeName)? GetIdInfo(
            ITypeCompletionContext completionContext,
            ArgumentDefinition definition)
        {
            var typeInspector = completionContext.TypeInspector;
            IDAttribute? idAttribute;
            IExtendedType? idType;

            if (definition is InputFieldDefinition inputField)
            {
                // UseSorting arg/s seems to come in here with a null Property
                if (inputField.Property is null)
                {
                    return null;
                }

                idAttribute = inputField.Property
                    .GetCustomAttributes(inherit: true)
                    .OfType<IDAttribute>()
                    .FirstOrDefault();

                idType = typeInspector.GetReturnType(inputField.Property, true);
            }
            else if (definition.Parameter is not null)
            {
                idAttribute = definition.Parameter
                    .GetCustomAttributes(inherit: true)
                    .OfType<IDAttribute>()
                    .FirstOrDefault();

                idType = typeInspector.GetArgumentType(definition.Parameter, true);
            }
            else
            {
                // Purely code-first fields/args with no backing CLR member: we can't determine
                // the runtime id type, so there's nothing for us to convert.
                return null;
            }

            if (idType is null)
            {
                return null;
            }

            if (idAttribute is null && !IsIdType(completionContext, definition))
            {
                return null;
            }

            var runtimeType = idType.ElementType?.Source ?? idType.Source;
            return (runtimeType, idAttribute?.TypeName);
        }

        // True if the field/argument's (already rewritten) GraphQL type is the relay IdType.
        // Both [ID] and the fluent `.ID()` rewrite the type to IdType before completion.
        private static bool IsIdType(
            ITypeCompletionContext completionContext,
            ArgumentDefinition definition)
            => definition.Type is ExtendedTypeReference typeReference
                && completionContext.TypeInspector
                    .CreateTypeInfo(typeReference.Type).NamedType == typeof(IdType);
    }
}
