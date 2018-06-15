using Bhp.IO;
using Bhp.IO.Caching;

namespace Bhp.UnitTests
{
    public class TestMetaDataCache<T> : MetaDataCache<T> where T : class, ISerializable, new()
    {
        public TestMetaDataCache()
            : base(null)
        {
        }

        protected override T TryGetInternal()
        {
            return null;
        }
    }
}
