using System;
using System.Linq;
using HotChocolate;
using HotChocolate.Configuration;
using HotChocolate.Internal;
using HotChocolate.Types;
using HotChocolate.Types.Descriptors;
using HotChocolate.Types.Descriptors.Configurations;
using HotChocolate.Types.Relay;
using Microsoft.Extensions.DependencyInjection;

namespace AutoGuru.HotChocolate.Types.Relay
{
    internal sealed class PolymorphicIdsTypeInterceptor : TypeInterceptor
    {
        private const string StandardGlobalIdFormatterName = "GlobalIdInputValueFormatter";

        // As of HC16, global ID support is tracked by an internal NodeSchemaFeature in the
        // feature collection (rather than the old "HotChocolate.Relay.GlobalId" context-data
        // key). We can't reference the internal type, so we detect it by name.
        private const string NodeSchemaFeatureName = "NodeSchemaFeature";

        private PolymorphicIdsOptions? _options;
        private PolymorphicIdsOptions Options =>
            _options ?? throw new Exception("Options weren't set up");

        public override void OnBeforeCompleteType(
            ITypeCompletionContext completionContext,
            TypeSystemConfiguration definition)
        {
            var globalIdSupportEnabled = completionContext.Features
                .Any(f => f.Key.Name == NodeSchemaFeatureName);
            if (!globalIdSupportEnabled)
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

            if (completionContext.Features.Get<PolymorphicIdsOptions>() is { } options)
            {
                _options = options;
            }

            if (definition is InputObjectTypeConfiguration inputObjectTypeConfiguration)
            {
                foreach (var inputFieldDefinition in inputObjectTypeConfiguration.Fields)
                {
                    var idRuntimeType = GetIdRuntimeType(completionContext, inputFieldDefinition);
                    if (idRuntimeType is not null && ShouldIntercept(idRuntimeType))
                    {
                        DeferFormatterReplacement(inputFieldDefinition, idRuntimeType);
                    }
                }
            }
            else if (definition is ObjectTypeConfiguration objectTypeConfiguration)
            {
                var isQueryType = definition.Name == OperationTypeNames.Query;

                foreach (var objectFieldDefinition in objectTypeConfiguration.Fields)
                {
                    if (isQueryType && objectFieldDefinition.Name == "node")
                    {
                        continue;
                    }

                    foreach (var argumentDefinition in objectFieldDefinition.Arguments)
                    {
                        var idRuntimeType = GetIdRuntimeType(completionContext, argumentDefinition);
                        if (idRuntimeType is not null && ShouldIntercept(idRuntimeType))
                        {
                            DeferFormatterReplacement(argumentDefinition, idRuntimeType);
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
            ArgumentConfiguration argumentConfiguration,
            Type idRuntimeType)
        {
            argumentConfiguration.Tasks.Add(
                new OnCompleteTypeSystemConfigurationTask(
                    (completionContext, _) => InsertFormatter(
                        completionContext,
                        argumentConfiguration,
                        idRuntimeType),
                    argumentConfiguration,
                    ApplyConfigurationOn.BeforeCompletion));
        }

        private static void InsertFormatter(
            ITypeCompletionContext completionContext,
            ArgumentConfiguration argumentConfiguration,
            Type idRuntimeType)
        {
            var formatter = new PolymorphicIdInputValueFormatter(
                idRuntimeType,
                completionContext.DescriptorContext.NodeIdSerializerAccessor);

            var formatters = argumentConfiguration.Formatters;
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

        // Resolves the CLR runtime type of an id field/argument, or null if it isn't a relay
        // ID we should handle. A field is treated as a relay ID if it carries the [ID]
        // attribute (attribute style) or its GraphQL type was rewritten to IdType - both the
        // attribute and the fluent `.ID()` declaration do this before completion, which is how
        // we now support fluent-style ids (see issue #5). The node type name is intentionally
        // not needed: the serializer parses by runtime type, not type name.
        private static Type? GetIdRuntimeType(
            ITypeCompletionContext completionContext,
            ArgumentConfiguration definition)
        {
            var typeInspector = completionContext.TypeInspector;
            bool hasIdAttribute;
            IExtendedType? idType;

            if (definition is InputFieldConfiguration inputField)
            {
                // UseSorting arg/s seems to come in here with a null Property
                if (inputField.Property is null)
                {
                    return null;
                }

                hasIdAttribute = inputField.Property
                    .GetCustomAttributes(inherit: true)
                    .Any(a => a is IDAttribute);

                idType = typeInspector.GetReturnType(inputField.Property, true);
            }
            else if (definition.Parameter is not null)
            {
                hasIdAttribute = definition.Parameter
                    .GetCustomAttributes(inherit: true)
                    .Any(a => a is IDAttribute);

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

            if (!hasIdAttribute && !IsIdType(completionContext, definition))
            {
                return null;
            }

            return idType.ElementType?.Source ?? idType.Source;
        }

        // True if the field/argument's (already rewritten) GraphQL type is the relay IdType.
        // Both [ID] and the fluent `.ID()` rewrite the type to IdType before completion.
        private static bool IsIdType(
            ITypeCompletionContext completionContext,
            ArgumentConfiguration definition)
            => definition.Type is ExtendedTypeReference typeReference
                && completionContext.TypeInspector
                    .CreateTypeInfo(typeReference.Type).NamedType == typeof(IdType);
    }
}
