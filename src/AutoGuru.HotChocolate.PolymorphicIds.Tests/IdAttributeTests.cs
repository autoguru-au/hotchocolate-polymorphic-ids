using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Threading.Tasks;
using HotChocolate;
using HotChocolate.Execution;
using HotChocolate.Execution.Options;
using HotChocolate.Types.Relay;
using Microsoft.Extensions.DependencyInjection;
using VerifyTests;
using VerifyXunit;
using Xunit;

namespace AutoGuru.HotChocolate.PolymorphicIds.Tests
{
    [UsesVerify]
    [SuppressMessage("Style", "IDE1006:Naming Styles")]
    [SuppressMessage("Performance", "CA1822:Mark members as static")]
    public class IdAttributeTests
    {
        private static readonly RequestExecutorOptions _executorOptions = new ()
        {
            ExecutionTimeout = TimeSpan.FromMinutes(1)
        };

        // TODO: When PR 3440 in HC is merged, uncomment array with nulls field usages below in queries

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public async Task PolyId_On_Arguments(bool isEnabled)
        {
            // arrange
            var intId = 1;
            var stringId = "abc";
            var guidId = new Guid("26a2dc8f-4dab-408c-88c6-523a0a89a2b5");
            var services = new ServiceCollection();
            var builder = services
                .AddGraphQL()
                .AddQueryType<Query>()
                .AddType<FooPayload>()
                .AddPolymorphicIds(isEnabled
                    ? default
                    : new PolymorphicIdsOptions
                    {
                        HandleGuidIds = false,
                        HandleIntIds = false,
                        HandleLongIds = false,
                        HandleStringIds = false,
                    })
                .ModifyRequestOptions(o => o.ExecutionTimeout = TimeSpan.FromMinutes(1));
            var executor = await builder
                .BuildRequestExecutorAsync();

            // act
            var result = await executor
                .ExecuteAsync(
                    QueryRequestBuilder.New()
                        .SetQuery(
                            @"query foo (
                                $intId: ID!
                                $nullIntId: ID = null
                                $stringId: ID!
                                $nullStringId: ID = null
                                $guidId: ID!
                                $nullGuidId: ID = null)
                            {
                                intId(id: $intId)
                                nullableIntId(id: $intId)
                                nullableIntIdGivenNull: nullableIntId(id: $nullIntId)
                                intIdList(id: [$intId])
                                # TODO: nullableIntIdList(id: [$intId, $nullIntId])
                                stringId(id: $stringId)
                                nullableStringId(id: $stringId)
                                nullableStringIdGivenNull: nullableStringId(id: $nullStringId)
                                stringIdList(id: [$stringId])
                                # TODO: nullableStringIdList(id: [$stringId, $nullStringId])
                                guidId(id: $guidId)
                                nullableGuidId(id: $guidId)
                                nullableGuidIdGivenNull: nullableGuidId(id: $nullGuidId)
                                guidIdList(id: [$guidId $guidId])
                                # TODO: nullableGuidIdList(id: [$guidId $nullGuidId $guidId])
                            }")
                        .SetVariableValue("intId", intId)
                        .SetVariableValue("stringId", stringId)
                        .SetVariableValue("guidId", guidId.ToString())
                        .Create());

            // assert
            var verifySettings = new VerifySettings();
            verifySettings.UseParameters(isEnabled);

            await Verifier.Verify(result.ToJson(), verifySettings);
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public async Task PolyId_On_Objects(bool isEnabled)
        {
            // arrange
            var someId = "1";
            var someIntId = 1;

            // act
            var result =
                await SchemaBuilder.New()
                    .AddQueryType<Query>()
                    .AddType<FooPayload>()
                    .AddPolymorphicIds(isEnabled
                        ? default
                        : new PolymorphicIdsOptions
                        {
                            HandleGuidIds = false,
                            HandleIntIds = false,
                            HandleLongIds = false,
                            HandleStringIds = false,
                        })
                    .Create()
                    .MakeExecutable(_executorOptions)
                    .ExecuteAsync(
                        QueryRequestBuilder.New()
                            .SetQuery(
                                @"query foo ($someId: ID! $someIntId: ID!) {
                                    foo(input: {
                                        someId: $someId
                                        someIds: [$someIntId]
                                        someNullableId: $someId
                                        someNullableIds: [$someIntId] }) # TODO: null] })
                                    {
                                        someId
                                        someNullableId
                                        ... on FooPayload {
                                            someIds
                                            someNullableIds
                                            raw
                                        }
                                    }
                                }")
                            .SetVariableValue("someId", someId)
                            .SetVariableValue("someNullableId", null)
                            .SetVariableValue("someIntId", someIntId)
                            .SetVariableValue("someNullableIntId", null)
                            .Create());

            // assert
            var verifySettings = new VerifySettings();
            verifySettings.UseParameters(isEnabled);

            await Verifier.Verify(result.ToJson(), verifySettings);
        }

        #region Standard ID still works

        [Fact]
        public async Task Id_On_Arguments()
        {
            // arrange
            var idSerializer = new IdSerializer();
            var intId = idSerializer.Serialize("Query", 1);
            var stringId = idSerializer.Serialize("Query", "abc");
            var guidId = idSerializer.Serialize("Query", new Guid("26a2dc8f-4dab-408c-88c6-523a0a89a2b5"));

            // act
            var result =
                await SchemaBuilder.New()
                    .AddQueryType<Query>()
                    .AddType<FooPayload>()
                    .Create()
                    .MakeExecutable()
                    .ExecuteAsync(
                        QueryRequestBuilder.New()
                            .SetQuery(
                                @"query foo (
                                    $intId: ID!
                                    $nullIntId: ID = null
                                    $stringId: ID!
                                    $nullStringId: ID = null
                                    $guidId: ID!
                                    $nullGuidId: ID = null)
                                {
                                    intId(id: $intId)
                                    nullableIntId(id: $intId)
                                    nullableIntIdGivenNull: nullableIntId(id: $nullIntId)
                                    intIdList(id: [$intId])
                                    # TODO: nullableIntIdList(id: [$intId, $nullIntId])
                                    stringId(id: $stringId)
                                    nullableStringId(id: $stringId)
                                    nullableStringIdGivenNull: nullableStringId(id: $nullStringId)
                                    stringIdList(id: [$stringId])
                                    # TODO: nullableStringIdList(id: [$stringId, $nullStringId])
                                    guidId(id: $guidId)
                                    nullableGuidId(id: $guidId)
                                    nullableGuidIdGivenNull: nullableGuidId(id: $nullGuidId)
                                    guidIdList(id: [$guidId $guidId])
                                    # TODO: nullableGuidIdList(id: [$guidId $nullGuidId $guidId])
                                }")
                            .SetVariableValue("intId", intId)
                            .SetVariableValue("stringId", stringId)
                            .SetVariableValue("guidId", guidId)
                            .Create());

            // assert
            await Verifier.Verify(result.ToJson());
        }

        [Fact]
        public async Task Id_On_Objects()
        {
            // arrange
            var idSerializer = new IdSerializer();
            var someId = idSerializer.Serialize("Some", "1");
            var someIntId = idSerializer.Serialize("Some", 1);

            // act
            var result =
                await SchemaBuilder.New()
                    .AddQueryType<Query>()
                    .AddType<FooPayload>()
                    .Create()
                    .MakeExecutable()
                    .ExecuteAsync(
                        QueryRequestBuilder.New()
                            .SetQuery(
                                @"query foo ($someId: ID! $someIntId: ID!) {
                                    foo(input: {
                                        someId: $someId
                                        someIds: [$someIntId]
                                        someNullableId: $someId
                                        someNullableIds: [$someIntId] })
                                    {
                                        someId
                                        someNullableId
                                        ... on FooPayload {
                                            someIds
                                            someNullableIds
                                        }
                                    }
                                }")
                            .SetVariableValue("someId", someId)
                            .SetVariableValue("someNullableId", null)
                            .SetVariableValue("someIntId", someIntId)
                            .SetVariableValue("someNullableIntId", null)
                            .Create());

            // assert
            await Verifier.Verify(new
            {
                result = result.ToJson(),
                someId,
                someIntId
            });
        }

        [Fact]
        public async Task Id_On_Objects_Given_Nulls()
        {
            // arrange
            var idSerializer = new IdSerializer();
            var someId = idSerializer.Serialize("Some", "1");
            var someIntId = idSerializer.Serialize("Some", 1);

            // act
            var result =
                await SchemaBuilder.New()
                    .AddQueryType<Query>()
                    .AddType<FooPayload>()
                    .Create()
                    .MakeExecutable()
                    .ExecuteAsync(
                        QueryRequestBuilder.New()
                            .SetQuery(
                                @"query foo ($someId: ID! $someIntId: ID!) {
                                    foo(input: {
                                        someId: $someId
                                        someIds: [$someIntId]
                                        someNullableId: null
                                        someNullableIds: [$someIntId] }) # TODO: null
                                    {
                                        someId
                                        someNullableId
                                        ... on FooPayload {
                                            someIds
                                            someNullableIds
                                        }
                                    }
                                }")
                            .SetVariableValue("someId", someId)
                            .SetVariableValue("someNullableId", null)
                            .SetVariableValue("someIntId", someIntId)
                            .SetVariableValue("someNullableIntId", null)
                            .Create());

            // assert
            await Verifier.Verify(new
            {
                result = result.ToJson(),
                someId,
                someIntId
            });
        }

        [Fact]
        public async Task Id_On_Objects_InvalidType()
        {
            // arrange
            var idSerializer = new IdSerializer();
            var someId = idSerializer.Serialize("Some", Guid.Empty);

            // act
            var result =
                await SchemaBuilder.New()
                    .AddQueryType<Query>()
                    .AddType<FooPayload>()
                    .Create()
                    .MakeExecutable()
                    .ExecuteAsync(
                        QueryRequestBuilder.New()
                            .SetQuery(
                                @"query foo ($someId: ID!) {
                                    foo(input: { someId: $someId someIds: [$someId] }) {
                                        someId
                                        ... on FooPayload {
                                            someIds
                                        }
                                    }
                                }")
                            .SetVariableValue("someId", someId)
                            .Create());

            // assert
            await Verifier.Verify(new
            {
                result = result.ToJson(),
                someId
            });
        }

        [Fact]
        public async Task Id_On_Objects_InvalidId()
        {
            // arrange
            var someId = "abc";

            // act
            var result =
                await SchemaBuilder.New()
                    .AddQueryType<Query>()
                    .AddType<FooPayload>()
                    .Create()
                    .MakeExecutable()
                    .ExecuteAsync(
                        QueryRequestBuilder.New()
                            .SetQuery(
                                @"query foo ($someId: ID!) {
                                    foo(input: { someId: $someId someIds: [$someId] }) {
                                        someId
                                        ... on FooPayload {
                                            someIds
                                        }
                                    }
                                }")
                            .SetVariableValue("someId", someId)
                            .Create());

            // assert
            await Verifier.Verify(new
            {
                result = result.ToJson(),
                someId
            });
        }

        #endregion

        public class Query
        {
            public string IntId([ID] int id) => id.ToString();
            public string IntIdList([ID] int[] id) =>
                string.Join(", ", id.Select(t => t.ToString()));

            public string NullableIntId([ID] int? id) => id?.ToString() ?? "null";
            public string NullableIntIdList([ID] int?[] id) =>
                string.Join(", ", id.Select(t => t?.ToString() ?? "null"));

            public string StringId([ID] string id) => id;
            public string StringIdList([ID] string[] id) =>
                string.Join(", ", id.Select(t => t.ToString()));

            public string NullableStringId([ID] string? id) => id ?? "null";
            public string NullableStringIdList([ID] string?[] id) =>
                string.Join(", ", id.Select(t => t?.ToString() ?? "null"));

            public string GuidId([ID] Guid id) => id.ToString();
            public string GuidIdList([ID] IReadOnlyList<Guid> id) =>
                string.Join(", ", id.Select(t => t.ToString()));

            public string NullableGuidId([ID] Guid? id) => id?.ToString() ?? "null";
            public string NullableGuidIdList([ID] IReadOnlyList<Guid?> id) =>
                string.Join(", ", id.Select(t => t?.ToString() ?? "null"));

            public IFooPayload Foo(FooInput input) =>
                new FooPayload(input.SomeId, input.SomeNullableId, input.SomeIds, input.SomeNullableIds);
        }

        public class FooInput
        {
            public FooInput(
                string someId,
                string? someNullableId,
                IReadOnlyList<int> someIds,
                IReadOnlyList<int?>? someNullableIds)
            {
                SomeId = someId;
                SomeNullableId = someNullableId;
                SomeIds = someIds;
                SomeNullableIds = someNullableIds;
            }

            [ID("Some")] public string SomeId { get; }

            [ID("Some")] public string? SomeNullableId { get; }

            [ID("Some")] public IReadOnlyList<int> SomeIds { get; }

            [ID("Some")] public IReadOnlyList<int?>? SomeNullableIds { get; }
        }

        public class FooPayload : IFooPayload
        {
            public FooPayload(
                string someId,
                string? someNullableId,
                IReadOnlyList<int> someIds,
                IReadOnlyList<int?>? someNullableIds)
            {
                SomeId = someId;
                SomeNullableId = someNullableId;
                SomeIds = someIds;
                SomeNullableIds = someNullableIds;
            }

            [ID("Bar")] public string SomeId { get; }

            [ID("Bar")] public IReadOnlyList<int> SomeIds { get; }

            [ID("Bar")] public string? SomeNullableId { get; }

            [ID("Bar")] public IReadOnlyList<int?>? SomeNullableIds { get; }

            public string Raw =>
                $"{nameof(SomeId)}: {SomeId}, " +
                $"{nameof(SomeIds)}: [{string.Join(", ", SomeIds)}], " +
                $"{nameof(SomeNullableId)}: {SomeNullableId}, " +
                $"{nameof(SomeNullableIds)}: [{string.Join(", ", SomeNullableIds ?? Array.Empty<int?>())}]";
        }

        public interface IFooPayload
        {
            [ID] string SomeId { get; }

            [ID] public string? SomeNullableId { get; }

            [ID] IReadOnlyList<int> SomeIds { get; }

            [ID] IReadOnlyList<int?>? SomeNullableIds { get; }

            string Raw { get; }
        }
    }
}
