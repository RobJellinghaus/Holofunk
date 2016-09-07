////////////////////////////////////////////////////////////////////////
// Copyright (c) 2011-2016 by Rob Jellinghaus.                        //
// All Rights Reserved.                                               //
////////////////////////////////////////////////////////////////////////

using Holofunk.Core;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;

namespace Holofunk.Tests
{
    /// <summary>Compare MethodInfos by name.</summary>
    class MethodInfoComparer : IComparer<MethodInfo>
    {
        internal static readonly MethodInfoComparer Instance = new MethodInfoComparer();

        public int Compare(MethodInfo x, MethodInfo y)
        {
            return StringComparer.InvariantCulture.Compare(x.Name, y.Name);
        }
    }

    public class TestSuite
    {
        Logger m_logger;

        protected TestSuite() { }

        public void RunAllTests()
        {
            List<MethodInfo> methods = new List<MethodInfo>(this.GetType().GetMethods());
            methods.Sort(MethodInfoComparer.Instance);
            foreach (MethodInfo method in methods) {
                if (method.Name.StartsWith("Test")) {
                    HoloDebug.Assert(method.GetParameters().Length == 0);
                    Debug.WriteLine(string.Format("Running test method {0}...", method.Name));
                    m_logger = new Logger();
                    method.Invoke(this, new object[] { });
                    m_logger = null;
                    Debug.WriteLine("    Success!");
                }
            }
        }

        protected Logger Log { get { return m_logger; } }
    }
}
