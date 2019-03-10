using System;
using System.Collections.Generic;
using System.Text;
using FluentAssertions;
using Xunit;

namespace Nkit.Proj.Tmpl.Tests
{
   public class Tpl_Tests
    {
        [Fact]
        public void tpl_add_test_for_something()
        {
            Tpl.Add(12, 13).Should().Be(25);

        }
    }
}
