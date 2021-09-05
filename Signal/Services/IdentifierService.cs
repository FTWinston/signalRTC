using Microsoft.Extensions.Options;
using Signal.Models;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Signal.Services
{
    public class IdentifierService
    {
        private IdentifierConfiguration Configuration { get; }

        private ProfanityFilter.ProfanityFilter ProfanityFilter { get; }

        // Store batches of IDs in this queue ... and push "released" ones onto the end.
        // Storing all possible IDs seems superfluous, but continuing from a batch seems tricky. Hmm.
        // Can we just store an iterator ... and it skips past in-use IDs?
        // Save off "the next few" in the queue for quick access, and schedule adding more when it gets low. I guess?

        private Queue<string> IdentifierQueue { get; } = new Queue<string>();

        public IdentifierService(IOptions<IdentifierConfiguration> configuration, ProfanityFilter.ProfanityFilter profanityFilter)
        {
            Configuration = configuration.Value;
            ProfanityFilter = profanityFilter;
        }

        private int NextID = 1;

        public string GenerateIdentifier()
        {
            // This is a temporary hack.
            return (NextID++).ToString();
        }

        public void ReleaseIdentifier(string identifier)
        {
            // TODO: something
        }

        public bool IsValidIdentifier(string identifier)
        {
            return !string.IsNullOrWhiteSpace(identifier)
                && identifier.Length >= Configuration.MinLength
                && !ProfanityFilter.IsProfanity(identifier);
        }
    }
}
