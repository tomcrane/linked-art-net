using LinkedArtNet;
using System.ComponentModel.DataAnnotations;
using System.Data;

namespace PmcTransformer.Tests
{
    public class ParsedAgentTests
    {
        public static TheoryData<ParsedAgent> ParsedAgentData => new TheoryData<ParsedAgent>
        {
            new ParsedAgent {
                Original = "Crane, Thomas [1971-] (developer)",
                NormalisedOriginal = "Crane, Thomas [1971-] (developer)",
                Name = "Crane, Thomas",
                DateString = "1971-",
                NumericDateString = null, // maybe should be "1971-"
                Role = "developer",
                Honorific = null,
                NormalisedName = "Crane, Thomas",
                NormalisedNameWithDates = "Crane, Thomas [1971-]",
                NormalisedFullForm = "Crane, Thomas [1971-] (developer)",
                NormalisedLocForm = "Crane, Thomas, 1971-",
                IsActive = false,
                IsApproximate = false,
                StartYear = 1971,
                EndYear = null
            },
            new ParsedAgent {
                Original = "Leconte de Lisle, Charles Marie [1818-1894] (author)",
                NormalisedOriginal = "Leconte de Lisle, Charles Marie [1818-1894] (author)",
                Name = "Leconte de Lisle, Charles Marie",
                DateString = "1818-1894",
                NumericDateString = null, // maybe should be "1971-"
                Role = "author",
                Honorific = null,
                NormalisedName = "Leconte de Lisle, Charles Marie",
                NormalisedNameWithDates = "Leconte de Lisle, Charles Marie [1818-1894]",
                NormalisedFullForm = "Leconte de Lisle, Charles Marie [1818-1894] (author)",
                NormalisedLocForm = "Leconte de Lisle, Charles Marie, 1818-1894",
                IsActive = false,
                IsApproximate = false,
                StartYear = 1818,
                EndYear = 1894
            },
            new ParsedAgent {
                Original = "Grierson, Herbert John Clifford [Sir, 1866-1960] (preface by)",
                NormalisedOriginal = "Grierson, Herbert John Clifford, Sir, [1866-1960] (preface by)",
                Name = "Grierson, Herbert John Clifford, Sir",
                DateString = "1866-1960",
                NumericDateString = null,
                Role = "preface by",
                Honorific = "Sir",
                NormalisedName = "Grierson, Herbert John Clifford, Sir",
                NormalisedNameWithDates = "Grierson, Herbert John Clifford, Sir, [1866-1960]",
                NormalisedFullForm = "Grierson, Herbert John Clifford, Sir, [1866-1960] (preface by)",
                NormalisedLocForm = "Grierson, Herbert John Clifford, Sir, 1866-1960",
                IsActive = false,
                IsApproximate = false,
                StartYear = 1866,
                EndYear = 1960
            },


        };


        [Theory]
        [MemberData(nameof(ParsedAgentData))]
        public void ParsedAgent_Understands_Honorific(ParsedAgent testAgent)
        {
            var agent = new ParsedAgent(testAgent.Original);
            Assert.Equal(agent.Original, testAgent.Original);
            Assert.Equal(agent.NormalisedOriginal, testAgent.NormalisedOriginal);
            Assert.Equal(agent.Name, testAgent.Name);
            Assert.Equal(agent.DateString, testAgent.DateString);
            Assert.Equal(agent.NumericDateString, testAgent.NumericDateString);
            Assert.Equal(agent.Role, testAgent.Role);
            Assert.Equal(agent.Honorific, testAgent.Honorific);
            Assert.Equal(agent.NormalisedName, testAgent.NormalisedName);
            Assert.Equal(agent.NormalisedNameWithDates, testAgent.NormalisedNameWithDates);
            Assert.Equal(agent.NormalisedFullForm, testAgent.NormalisedFullForm);
            Assert.Equal(agent.NormalisedLocForm, testAgent.NormalisedLocForm);
            Assert.Equal(agent.IsActive, testAgent.IsActive);
            Assert.Equal(agent.IsApproximate, testAgent.IsApproximate);
            Assert.Equal(agent.StartYear, testAgent.StartYear);
            Assert.Equal(agent.EndYear, testAgent.EndYear);
        }
    }

}