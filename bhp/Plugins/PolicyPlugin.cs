﻿using Bhp.Core;
using System.Collections.Generic;

namespace Bhp.Plugins
{
    public abstract class PolicyPlugin : Plugin
    {
        private static readonly List<PolicyPlugin> instances = new List<PolicyPlugin>();

        public new static IEnumerable<PolicyPlugin> Instances => instances;

        protected PolicyPlugin()
        {
            instances.Add(this);
        }

        internal protected abstract bool CheckPolicy(Transaction tx);
    }
}
