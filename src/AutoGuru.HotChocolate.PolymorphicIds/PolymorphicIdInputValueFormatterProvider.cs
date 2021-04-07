using System;
using System.Collections.Concurrent;
using HotChocolate;
using HotChocolate.Types.Relay;
using Microsoft.Extensions.DependencyInjection;

namespace AutoGuru.HotChocolate.Types.Relay
{
    internal static class PolymorphicIdInputValueFormatterProvider
    {
        private static ConcurrentDictionary<NameString, ConcurrentDictionary<Type, PolymorphicIdInputValueFormatter>> _cache = new();

        internal static PolymorphicIdInputValueFormatter Get(
            IServiceProvider serviceProvider,
            NameString typeName,
            Type idRuntimeType)
        {
            var innerCache = _cache.GetOrAdd(
                typeName,
                new ConcurrentDictionary<Type, PolymorphicIdInputValueFormatter>());

            return innerCache.GetOrAdd(
                idRuntimeType,
                new PolymorphicIdInputValueFormatter(
                    typeName,
                    idRuntimeType,
                    GetIdSerializer(serviceProvider)));
        }

        private static IIdSerializer? _idSerializer;
        private static IIdSerializer GetIdSerializer(IServiceProvider services)
        {
            if (_idSerializer == null)
            {
                _idSerializer =
                    services.GetService<IIdSerializer>() ??
                    new IdSerializer();
            }
            return _idSerializer;
        }
    }
}
