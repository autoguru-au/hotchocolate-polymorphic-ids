﻿using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Threading.Tasks;
using HotChocolate;
using HotChocolate.Execution;
using HotChocolate.Execution.Options;
using HotChocolate.Types;
using HotChocolate.Types.Relay;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using VerifyTests;
using VerifyXunit;
using Xunit;

namespace AutoGuru.HotChocolate.PolymorphicIds.Tests
{
    [UsesVerify]
    [SuppressMessage("Style", "IDE1006:Naming Styles")]
    public class IdAttributeTests
    {
        private static readonly RequestExecutorOptions _executorOptions = new ()
        {
            ExecutionTimeout = TimeSpan.FromMinutes(1),
            IncludeExceptionDetails = true
        };

        private const string _argumentsQuery = @"
            query foo (
                $intId: ID!
                $longId: ID!
                $stringId: ID!
                $guidId: ID!
                $null: ID = null)
            {
                intId(id: $intId)
                nullableIntId(id: $intId)
                nullableIntIdGivenNull: nullableIntId(id: $null)
                intIdList(id: [$intId])
                nullableIntIdList(id: [$intId, $null])

                longId(id: $longId)
                nullableLongId(id: $longId)
                nullableLongIdGivenNull: nullableLongId(id: $null)
                longIdList(id: [$longId])
                nullableLongIdList(id: [$longId, $null])

                stringId(id: $stringId)
                nullableStringId(id: $stringId)
                nullableStringIdGivenNull: nullableStringId(id: $null)
                stringIdList(id: [$stringId])
                nullableStringIdList(id: [$stringId, $null])

                guidId(id: $guidId)
                nullableGuidId(id: $guidId)
                nullableGuidIdGivenNull: nullableGuidId(id: $null)
                guidIdList(id: [$guidId $guidId])
                nullableGuidIdList(id: [$guidId $null $guidId])
            }";

        [Fact]
        public async Task PolyId_SchemaBuildError_If_GlobalIdentification_Isnt_Enabled()
        {
            // arrange

            // act
            var schemaBuilder = SchemaBuilder.New()
                .AddQueryType<Query>()
                .AddPolymorphicIds();

            // assert
            var ex = Should.Throw<SchemaException>(() => schemaBuilder.Create());
            await Verifier.Verify(ex.Message);
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public async Task PolyId_On_Arguments(bool isEnabled)
        {
            // arrange
            var intId = 1;
            var longId = long.MaxValue;
            var stringId = "abc";
            var guidId = new Guid("26a2dc8f-4dab-408c-88c6-523a0a89a2b5");
            var services = new ServiceCollection();
            var builder = services
                .AddGraphQL()
                .AddQueryType<Query>()
                .AddType<FooPayload>()
                .AddType<Bar>()
                .AddGlobalObjectIdentification(registerNodeInterface: false)
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
                        .SetQuery(_argumentsQuery)
                        .SetVariableValue("intId", intId)
                        .SetVariableValue("longId", longId.ToString())
                        .SetVariableValue("stringId", stringId)
                        .SetVariableValue("guidId", guidId.ToString())
                        .Create());

            // assert
            var verifySettings = new VerifySettings();
            verifySettings.UseParameters(isEnabled);

            await Verifier.Verify(result.ToJson(), verifySettings);
        }

        [Fact]
        public async Task PolyId_On_Arguments_Invalid_Id()
        {
            // arrange

            // act
            var result =
                await SchemaBuilder.New()
                    .AddQueryType<Query>()
                    .AddType<FooPayload>()
                    .AddGlobalObjectIdentification(registerNodeInterface: false)
                    .AddPolymorphicIds()
                    .Create()
                    .MakeExecutable(_executorOptions)
                    .ExecuteAsync(
                        QueryRequestBuilder.New()
                            .SetQuery(
                                @"query foo {
                                    intId(id: ""SomethingInvalid"")
                                }")
                            .Create());

            // assert
            await Verifier.Verify(result.ToJson());
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
                    .AddGlobalObjectIdentification(registerNodeInterface: false)
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
                                        someNullableIds: [$someIntId, null]
                                    })
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

        [Fact]
        public async Task PolyId_On_Objects_Invalid_Id()
        {
            // arrange
            var someId = "1";

            // act
            var result =
                await SchemaBuilder.New()
                    .AddQueryType<Query>()
                    .AddType<FooPayload>()
                    .AddGlobalObjectIdentification(registerNodeInterface: false)
                    .AddPolymorphicIds()
                    .Create()
                    .MakeExecutable(_executorOptions)
                    .ExecuteAsync(
                        QueryRequestBuilder.New()
                            .SetQuery(
                                @"query foo ($someId: ID!) {
                                    foo(input: {
                                        someId: $someId
                                        someIds: [""SomethingInvalid""] })
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
                            .Create());

            // assert
            await Verifier.Verify(result.ToJson());
        }

        [Fact(Skip = "WIP, not yet supported")]
        public async Task PolyId_On_Objects_Invalid_Id_FluentStyle()
        {
            // arrange
            var someId = "1";

            // act
            var schema =
                SchemaBuilder.New()
                    .AddQueryType<Query>()
                    .AddType<FooPayload>()
                    .AddType<LolInputType>()
                    .AddGlobalObjectIdentification(registerNodeInterface: false)
                    .AddPolymorphicIds()
                    .Create();

            var result = await schema
                    .MakeExecutable(_executorOptions)
                    .ExecuteAsync(
                        QueryRequestBuilder.New()
                            .SetQuery(
                                @"query lol ($someId: ID!) {
                                    lol(input: {
                                        someIdCustomName: $someId })
                                }")
                            .SetVariableValue("someId", someId)
                            .Create());

            // assert
            await Verifier.Verify(
                new
                {
                    schema = schema.ToString(),
                    result = result.ToJson()
                });
        }

        #region Standard ID still works

        [Fact]
        public async Task Id_On_Arguments()
        {
            // arrange
            var idSerializer = new IdSerializer();
            var intId = idSerializer.Serialize("Some", 1);
            var longId = idSerializer.Serialize("Some", long.MaxValue);
            var stringId = idSerializer.Serialize("Some", "abc");
            var guidId = idSerializer.Serialize("Some", new Guid("26a2dc8f-4dab-408c-88c6-523a0a89a2b5"));

            // act
            var result =
                await SchemaBuilder.New()
                    .AddQueryType<Query>()
                    .AddType<FooPayload>()
                    .AddType<Bar>()
                    .AddGlobalObjectIdentification(registerNodeInterface: false)
                    .Create()
                    .MakeExecutable()
                    .ExecuteAsync(
                        QueryRequestBuilder.New()
                            .SetQuery(_argumentsQuery)
                            .SetVariableValue("intId", intId)
                            .SetVariableValue("longId", longId?.ToString())
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
                    .AddGlobalObjectIdentification(registerNodeInterface: false)
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
                                        someNullableIds: [$someIntId]
                                    })
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
                    .AddGlobalObjectIdentification(registerNodeInterface: false)
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
                                        someNullableIds: [$someIntId, null]
                                    })
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
                    .AddGlobalObjectIdentification(registerNodeInterface: false)
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
            var schema = SchemaBuilder.New()
                .AddQueryType<Query>()
                .AddType<FooPayload>()
                .AddGlobalObjectIdentification(registerNodeInterface: false)
                .Create();

            var executableSchema = schema
                .MakeExecutable();

            var result = await executableSchema
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

        [Fact]
        public async Task Schema()
        {
            // arrange / act
            var schema =
                SchemaBuilder.New()
                    .AddQueryType<Query>()
                    .AddType<FooPayload>()
                    .AddGlobalObjectIdentification(registerNodeInterface: false)
                    .AddPolymorphicIds()
                    .Create()
                    .ToString();

            // assert
            await Verifier.Verify(schema);
        }

        [SuppressMessage("Performance", "CA1822:Mark members as static", Justification = "Can't be static for HC")]
        public class Query
        {
            public string IntId([ID] int id) => id.ToString();
            public string IntIdList([ID] int[] id) =>
                string.Join(", ", id.Select(t => t.ToString()));

            public string NullableIntId([ID] int? id) => id?.ToString() ?? "null";
            public string NullableIntIdList([ID] int?[] id) =>
                string.Join(", ", id.Select(t => t?.ToString() ?? "null"));

            public string LongId([ID] long id) => id.ToString();
            public string LongIdList([ID] long[] id) =>
                string.Join(", ", id.Select(t => t.ToString()));

            public string NullableLongId([ID] long? id) => id?.ToString() ?? "null";
            public string NullableLongIdList([ID] long?[] id) =>
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

            public int Lol(LolInput input) => input.SomeId;
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
                $"{nameof(SomeNullableId)}: {SomeNullableId ?? "null"}, " +
                $"{nameof(SomeNullableIds)}: {(SomeNullableIds == null ? "null" : "[" + string.Join(", ", SomeNullableIds.Select(x => x?.ToString() ?? "null").ToArray()) + "]")}";
        }

        public interface IFooPayload
        {
            [ID] string SomeId { get; }

            [ID] public string? SomeNullableId { get; }

            [ID] IReadOnlyList<int> SomeIds { get; }

            [ID] IReadOnlyList<int?>? SomeNullableIds { get; }

            string Raw { get; }
        }

        [Node]
        public class Bar
        {
            public int Id { get; }

            public Bar(int id)
            {
                Id = id;
            }

            public static Bar GetBarAsync(int id) => new(id);
        }

        public class LolInput
        {
            public int SomeId { get; set; }
        }

        public class LolInputType : InputObjectType<LolInput>
        {
            protected override void Configure(IInputObjectTypeDescriptor<LolInput> descriptor)
            {
                descriptor.Field(x => x.SomeId).Name("someIdCustomName").ID("Some");
            }
        }
    }
}
