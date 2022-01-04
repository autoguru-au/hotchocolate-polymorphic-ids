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

namespace AutoGuru.HotChocolate.Types.Relay
{
    internal sealed class PolymorphicIdsTypeInterceptor : TypeInterceptor
    {
        private const string StandardGlobalIdFormatterName = "GlobalIdInputValueFormatter";

        private PolymorphicIdsOptions? _options;
        private PolymorphicIdsOptions Options =>
            _options ?? throw new Exception("Options weren't set up");

        public override void OnBeforeCompleteType(
            ITypeCompletionContext completionContext,
            DefinitionBase? definition,
            IDictionary<string, object?> contextData)
        {
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
            NameString typeName,
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

        private static (string NodeTypeName, Type IdRuntimeType)? GetIdInfo(
            ITypeCompletionContext completionContext,
            ArgumentDefinition definition)
        {
            var typeInspector = completionContext.TypeInspector;
            IDAttribute? idAttribute = null;
            IExtendedType? idType = null;

            if (definition is ArgumentDefinition argument)
            {
                if (argument.Parameter == null)
                {
                    return null;
                }

                idAttribute = (IDAttribute?)argument.Parameter
                   .GetCustomAttributes(inherit: true)
                   .SingleOrDefault(a => a is IDAttribute);
                if (idAttribute == null)
                {
                    return null;
                }

                idType = typeInspector.GetArgumentType(argument.Parameter, true);
            }
            else if (definition is InputFieldDefinition inputField)
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
                : completionContext.Type.Name;

            return (nodeTypeName, idRuntimeType);
        }
    }
}
