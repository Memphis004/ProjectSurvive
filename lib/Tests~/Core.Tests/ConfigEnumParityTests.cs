using System;
using System.Collections.Generic;
using System.Linq;
using ProjectSurvive.Core.Domain.Config;
using Xunit;

namespace ProjectSurvive.Core.Tests
{
    /// <summary>
    /// Guards the promise made in <c>lib/Runtime/Domain/Config/ConfigEnums.cs</c>:
    /// the domain enums are hand-written mirrors of the Luban enums declared in
    /// <c>data/Defines/schema.xml</c>, and the Infrastructure adapter converts
    /// between them with a plain cast.
    /// </summary>
    /// <remarks>
    /// A cast is only correct while the names AND the numeric values agree. If
    /// someone renumbers a value in the schema and regenerates, this test fails
    /// instead of the game silently reading one language as another. Only the test assembly
    /// may reference both sides - Domain keeps its own copy precisely so it never
    /// references generated code (ADR 0006).
    /// </remarks>
    public sealed class ConfigEnumParityTests
    {
        /// <summary>Every domain enum and its generated counterpart.</summary>
        private static readonly (Type Domain, Type Generated)[] Pairs =
        {
            (typeof(Language), typeof(ProjectSurvive.Config.Language)),
        };

        [Fact]
        public void Domain_enums_mirror_the_generated_enums_exactly()
        {
            var problems = new List<string>();

            foreach ((Type domain, Type generated) in Pairs)
            {
                Dictionary<string, int> expected = Values(domain);
                Dictionary<string, int> actual = Values(generated);

                foreach (KeyValuePair<string, int> pair in expected)
                {
                    if (!actual.TryGetValue(pair.Key, out int value))
                    {
                        problems.Add(generated.Name + " is missing '" + pair.Key + "'");
                    }
                    else if (value != pair.Value)
                    {
                        problems.Add(
                            generated.Name + "." + pair.Key + " is " + value + ", the domain says " + pair.Value);
                    }
                }

                foreach (string name in actual.Keys.Where(name => !expected.ContainsKey(name)))
                {
                    problems.Add(domain.Name + " is missing '" + name + "' (schema has it)");
                }
            }

            Assert.Empty(problems);
        }

        [Fact]
        public void Every_domain_enum_under_test_still_exists()
        {
            // Guards the table above: a renamed enum must be updated here rather
            // than silently dropped from the parity check.
            Assert.Single(Pairs);
            Assert.All(Pairs, pair =>
            {
                Assert.True(pair.Domain.IsEnum, pair.Domain.FullName);
                Assert.True(pair.Generated.IsEnum, pair.Generated.FullName);
                Assert.NotEmpty(Values(pair.Domain));
            });
        }

        private static Dictionary<string, int> Values(Type type)
        {
            var values = new Dictionary<string, int>(StringComparer.Ordinal);
            foreach (string name in Enum.GetNames(type))
            {
                values[name] = Convert.ToInt32(Enum.Parse(type, name), System.Globalization.CultureInfo.InvariantCulture);
            }

            return values;
        }
    }
}
