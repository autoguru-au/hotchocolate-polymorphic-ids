using System;
using System.Collections.Generic;
using System.Linq;
using HotChocolate;
using HotChocolate.Configuration;
using HotChocolate.Internal;
using HotChocolate.Types;
using HotChocolate.Types.Descriptors;
using HotChocolate.Types.Descriptors.Definitions;
using HotChocolate.Types.Relay;
using Microsoft.Extensions.DependencyInjection;

namespace AutoGuru.HotChocolate.PolymorphicIds
{
    internal sealed class PolymorphicIdsTypeInterceptor : TypeInterceptor
    {
        private const string StandardGlobalIdFormatterName = "GlobalIdInputValueFormatter";

        // /src/HotChocolate/Core/src/Types/Types/WellKnownContextData.cs
        private const string GlobalIdSupportEnabledContextDataKey = "HotChocolate.Relay.GlobalId";

        private PolymorphicIdsOptions? _options;
        private PolymorphicIdsOptions Options =>
            _options ?? throw new Exception("Options weren't set up");

        public override void OnBeforeCompleteType(
            ITypeCompletionContext completionContext,
            DefinitionBase? definition,
            IDictionary<string, object?> contextData)
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
                        InsertFormatter(
                            completionContext,
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
                            InsertFormatter(
                                completionContext,
                                argumentDefinition,
                                idInfo.Value.NodeTypeName,
                                idInfo.Value.IdRuntimeType);
                        }
                    }
                }
            }

            base.OnBeforeCompleteType(completionContext, definition, contextData);
        }

        private static void InsertFormatter(
            ITypeCompletionContext completionContext,
            ArgumentDefinition argumentDefinition,
            NameString? typeName,
            Type idRuntimeType)
        {
            var formatter = PolymorphicIdInputValueFormatterProvider.Get(
                completionContext.Services,
                typeName,
                idRuntimeType);

            var defaultFormatter = argumentDefinition.Formatters
                .FirstOrDefault(f => f.GetType().Name == StandardGlobalIdFormatterName);

            if (defaultFormatter == null)
            {
                argumentDefinition.Formatters.Insert(0, formatter);
            }
            else
            {
                argumentDefinition.Formatters.Insert(
                    argumentDefinition.Formatters.IndexOf(defaultFormatter) - 1,
                    formatter);
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

        private static (string? NodeTypeName, Type IdRuntimeType)? GetIdInfo(
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
                    // TODO: Here you could check for
                    //var idNullableType = completionContext.TypeInspector.GetTypeRef(typeof(IdType));
                    //var idNonNullableType = completionContext.TypeInspector.GetTypeRef(typeof(NonNullType<IdType>));
                    //(inputField.Type as ExtendedTypeReference).Type == idNonNullableType.Type || idNullableType.Type
                    // and then we know it's an id but then the problem is how to get the type name (if any)

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
            string nodeTypeName = idAttribute?.TypeName.HasValue ?? false
                ? idAttribute.TypeName
                : null;

            return (nodeTypeName, idRuntimeType);
        }
    }
}
