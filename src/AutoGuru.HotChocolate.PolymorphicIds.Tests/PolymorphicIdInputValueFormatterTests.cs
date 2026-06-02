using System.Collections.Generic;
using AutoGuru.HotChocolate.Types.Relay;
using HotChocolate;
using HotChocolate.Types.Relay;
using Shouldly;
using Xunit;

namespace AutoGuru.HotChocolate.PolymorphicIds.Tests
{
    public class PolymorphicIdInputValueFormatterTests
    {
        private sealed class StubAccessor : INodeIdSerializerAccessor
        {
            public INodeIdSerializer Serializer { get; } = new DefaultNodeIdSerializer();
        }

        private static PolymorphicIdInputValueFormatter CreateSut(
            System.Type idRuntimeType, string? expectedTypeName = null) =>
            new(idRuntimeType, expectedTypeName, new StubAccessor());

        [Fact]
        public void Format_Null_Returns_Null()
        {
            var sut = CreateSut(typeof(int));

            sut.Format(null).ShouldBeNull();
        }

        [Fact]
        public void Format_NodeId_Returns_InternalId()
        {
            var sut = CreateSut(typeof(int));

            // A NodeId that arrives already-parsed should be unwrapped to its internal id.
            sut.Format(new NodeId("Some", 42)).ShouldBe(42);
        }

        [Fact]
        public void Format_Unhandled_RuntimeValue_Falls_Through_Unchanged()
        {
            var sut = CreateSut(typeof(int));

            // Anything that isn't a string / string list / NodeId is left as-is.
            var alreadyInternal = 123;
            sut.Format(alreadyInternal).ShouldBe(alreadyInternal);
        }

        [Fact]
        public void Format_RawIntString_Returns_Int()
        {
            var sut = CreateSut(typeof(int));

            sut.Format("1").ShouldBe(1);
        }

        [Fact]
        public void Format_SerializedGlobalId_Is_Deserialized()
        {
            var serializer = new DefaultNodeIdSerializer();
            var globalId = serializer.Format("Some", 7);
            var sut = CreateSut(typeof(int));

            // Genuine global ids must still be accepted (backwards compatible).
            sut.Format(globalId).ShouldBe(7);
        }

        [Fact]
        public void Format_StringList_With_Nulls_Preserves_Nulls()
        {
            var sut = CreateSut(typeof(int?));

            var result = sut.Format(new List<string?> { "1", null, "2" });

            result.ShouldBeAssignableTo<int?[]>();
            ((int?[])result!).ShouldBe(new int?[] { 1, null, 2 });
        }

        [Fact]
        public void Format_GlobalId_With_Matching_TypeName_Is_Accepted()
        {
            var globalId = new DefaultNodeIdSerializer().Format("Some", 7);
            var sut = CreateSut(typeof(int), expectedTypeName: "Some");

            sut.Format(globalId).ShouldBe(7);
        }

        [Fact]
        public void Format_GlobalId_With_Wrong_TypeName_Throws()
        {
            var globalId = new DefaultNodeIdSerializer().Format("Other", 7);
            var sut = CreateSut(typeof(int), expectedTypeName: "Some");

            Should.Throw<GraphQLException>(() => sut.Format(globalId));
        }

        [Fact]
        public void Format_RawId_Bypasses_TypeName_Validation()
        {
            // Raw db ids carry no type name, so they're accepted even when an explicit
            // type name is expected (this is the whole point of the library).
            var sut = CreateSut(typeof(int), expectedTypeName: "Some");

            sut.Format("1").ShouldBe(1);
        }
    }
}
