using System;
using System.Collections.Generic;
using HotChocolate;
using HotChocolate.Types;
using HotChocolate.Types.Relay;

namespace AutoGuru.HotChocolate.Types.Relay
{
    internal class PolymorphicIdInputValueFormatter : IInputValueFormatter
    {
        private readonly NameString _schemaName;
        private readonly NameString _nodeTypeName;
        private readonly Type _idRuntimeType;
        private readonly IIdSerializer _idSerializer;

        public PolymorphicIdInputValueFormatter(
            NameString nodeTypeName,
            Type idRuntimeType,
            IIdSerializer idSerializer)
        {
            _schemaName = null; // not needed during deserialization
            _nodeTypeName = nodeTypeName;
            _idRuntimeType = idRuntimeType;
            _idSerializer = idSerializer;
        }

        public object? OnAfterDeserialize(object? runtimeValue)
        {
            if (runtimeValue is null)
            {
                return null;
            }

            if (runtimeValue is string s)
            {
                return DeserializeId(s);
            }

            if (runtimeValue is IEnumerable<string> stringEnumerable) // TODO: When PR 3440 in HC is merged, this should become IEnumerable<string?>
            {
                try
                {
                    var list = new List<IdValue>();// TODO: When PR 3440 in HC is merged, this should become List<IdValue?>
                    foreach (var sv in stringEnumerable)
                    {
                        // TODO: When PR 3440 in HC is merged, this should be uncommented
                        //if (sv is null)
                        //{
                        //  list.Add(null);
                        //}
                        //else
                        //{
                        list.Add(DeserializeId(sv));
                        //}
                    }
                    return list;
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

            // Let fall through to default formatter
            return runtimeValue;
        }

        private IdValue DeserializeId(string value)
        {
            if (value is null)
            {
                throw new ArgumentNullException(nameof(value));
            }

            if ((_idRuntimeType == typeof(int) || _idRuntimeType == typeof(int?)) &&
                value is string rawIntString &&
                int.TryParse(rawIntString, out var intValue))
            {
                return new IdValue(_schemaName, _nodeTypeName, intValue);
            }

            if ((_idRuntimeType == typeof(long) || _idRuntimeType == typeof(long?)) &&
                value is string rawLongString &&
                long.TryParse(rawLongString, out var longValue))
            {
                return new IdValue(_schemaName, _nodeTypeName, longValue);
            }

            if ((_idRuntimeType == typeof(Guid) || _idRuntimeType == typeof(Guid?)) &&
                value is string rawGuidString &&
                Guid.TryParse(rawGuidString, out var guidValue))
            {
                return new IdValue(_schemaName, _nodeTypeName, guidValue);
            }

            try
            {
                return _idSerializer.Deserialize(value);
            }
            catch
            {
                // If the runtime type is a string,
                // allow to fall through as this is likely a non-serialized id.
                // There is a slight chance it's not, but we let it slide
                if (_idRuntimeType == typeof(string))
                {
                    return new IdValue(_schemaName, _nodeTypeName, value);
                }
            }

            throw new GraphQLException(
                ErrorBuilder.New()
                    .SetMessage("The ID `{0}` has an invalid format.", value)
                    .Build());
        }
    }
}
