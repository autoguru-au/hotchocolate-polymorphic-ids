using HotChocolate.Types.Relay;

namespace Microsoft.Extensions.DependencyInjection
{
    public class PolymorphicIdsOptions
    {
        /// <summary>
        /// If true, ids that have a runtime type of <c>Guid</c> or <c>Guid?</c>
        /// will be intercepted and attempted to be parsed directly as a <c>Guid</c>
        /// before being parsed as usual as a fully global id.
        /// </summary>
        public bool HandleGuidIds { get; set; } = true;

        /// <summary>
        /// If true, ids that have a runtime type of <c>int</c> or <c>int?</c>
        /// will be intercepted and attempted to be parsed directly as an <c>int</c>
        /// before being parsed as usual as a fully global id.
        /// </summary>
        public bool HandleIntIds { get; set; } = true;

        /// <summary>
        /// If true, ids that have a runtime type of <c>long</c> or <c>long?</c>
        /// will be intercepted and attempted to be parsed directly as a long
        /// before being parsed as usual as a fully global id.
        /// </summary>
        public bool HandleLongIds { get; set; } = true;

        /// <summary>
        /// If true, ids that have a runtime type of <c>string</c> or <c>string?</c>
        /// will be considered as as id value (dbId) if the <see cref="IIdSerializer"/>
        /// fails to parse it as a fully global id.
        ///
        /// Important: This will also allow invalid base64 strings to be incorrectly considered
        /// as the database id, but unfortunately this case is impossible to differentiate.
        /// </summary>
        public bool HandleStringIds { get; set; } = true;
    }
}
