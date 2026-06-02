using System;
using System.Collections.Generic;
using HotChocolate;
using HotChocolate.Types;
using HotChocolate.Types.Relay;

namespace AutoGuru.HotChocolate.Types.Relay
{
    internal sealed class PolymorphicIdInputValueFormatter : IInputValueFormatter
    {
        private readonly Type _idRuntimeType;
        private readonly Type _underlyingIdRuntimeType;
        private readonly INodeIdSerializerAccessor _serializerAccessor;
        private INodeIdSerializer? _serializer;

        public PolymorphicIdInputValueFormatter(
            Type idRuntimeType,
            INodeIdSerializerAccessor serializerAccessor)
        {
            _idRuntimeType = idRuntimeType;
            _underlyingIdRuntimeType = Nullable.GetUnderlyingType(idRuntimeType) ?? idRuntimeType;
            _serializerAccessor = serializerAccessor;
        }

        public object? Format(object? runtimeValue)
        {
            if (runtimeValue is null)
            {
                return null;
            }

            // A single id value, e.g. "1" or a fully serialized global id string.
            if (runtimeValue is string s)
            {
                return DeserializeId(s);
            }

            // A list of id values. We replace Hot Chocolate's GlobalIdInputValueFormatter
            // (rather than chaining before it) so we must produce the final internal ids
            // here, as a strongly-typed array of the element runtime type. This mirrors
            // the contract of the built-in formatter (HotChocolate.Types.Relay) and
            // correctly preserves nulls for nullable id lists.
            if (runtimeValue is IEnumerable<string?> stringEnumerable)
            {
                try
                {
                    var values = new List<object?>();
                    foreach (var sv in stringEnumerable)
                    {
                        values.Add(sv is null ? null : DeserializeId(sv));
                    }

                    var result = Array.CreateInstance(_idRuntimeType, values.Count);
                    for (var i = 0; i < values.Count; i++)
                    {
                        result.SetValue(values[i], i);
                    }
                    return result;
                }
                catch
                {
                    throw new GraphQLException(
                        ErrorBuilder.New()
                            .SetMessage(
                                "The IDs `{0}` have an invalid format.",
                                string.Join(", ", stringEnumerable))
                            .Build());
                }
            }

            // Already an internal id (e.g. a NodeId produced upstream) - unwrap it.
            if (runtimeValue is NodeId nodeId)
            {
                return nodeId.InternalId;
            }

            // Let anything else fall through unchanged.
            return runtimeValue;
        }

        private object DeserializeId(string value)
        {
            if (_underlyingIdRuntimeType == typeof(int) &&
                int.TryParse(value, out var intValue))
            {
                return intValue;
            }

            if (_underlyingIdRuntimeType == typeof(long) &&
                long.TryParse(value, out var longValue))
            {
                return longValue;
            }

            if (_underlyingIdRuntimeType == typeof(Guid) &&
                Guid.TryParse(value, out var guidValue))
            {
                return guidValue;
            }

            _serializer ??= _serializerAccessor.Serializer;

            try
            {
                return _serializer.Parse(value, _underlyingIdRuntimeType).InternalId;
            }
            catch
            {
                // If the runtime type is a string, allow it to fall through as this is
                // likely a non-serialized (database) id. There is a slight chance it's
                // not, but we let it slide.
                if (_underlyingIdRuntimeType == typeof(string))
                {
                    return value;
                }
            }

            throw new GraphQLException(
                ErrorBuilder.New()
                    .SetMessage("The ID `{0}` has an invalid format.", value)
                    .Build());
        }
    }
}
