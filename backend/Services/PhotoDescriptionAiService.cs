using System.Text.Json;
using Anthropic;
using Anthropic.Models.Messages;
using PhotoLibApi.Models;

namespace PhotoLibApi.Services
{
    /// <summary>
    /// Drafts a title, description and tags for a photo using Claude vision
    /// plus web search, so the description can reference real facts about the
    /// photographed subject rather than just what's visible in the pixels.
    /// Nothing is persisted here - the caller decides whether to save the draft.
    /// </summary>
    public class PhotoDescriptionAiService
    {
        private readonly AnthropicClient _client;
        private readonly PhotoFilePathHelper _filePathHelper;

        public PhotoDescriptionAiService(AnthropicClient client, PhotoFilePathHelper filePathHelper)
        {
            _client = client;
            _filePathHelper = filePathHelper;
        }

        public async Task<GenerateDescriptionResponse> GenerateAsync(
            Photo photo,
            GenerateDescriptionRequest request,
            CancellationToken cancellationToken = default)
        {
            var imagePath = photo.HasOriginal
                ? _filePathHelper.GetOriginalFilePath(photo.Id)
                : _filePathHelper.GetThumbnailFilePath(photo.Id);

            if (!File.Exists(imagePath))
                throw new FileNotFoundException("Photo has no image file to analyze.", imagePath);

            var imageBase64 = Convert.ToBase64String(await File.ReadAllBytesAsync(imagePath, cancellationToken));

            var schema = new Dictionary<string, JsonElement>
            {
                ["type"] = JsonSerializer.SerializeToElement("object"),
                ["properties"] = JsonSerializer.SerializeToElement(new
                {
                    title = new { type = "string" },
                    description = new { type = "string" },
                    tags = new { type = "array", items = new { type = "string" } },
                }),
                ["required"] = JsonSerializer.SerializeToElement(new[] { "title", "description", "tags" }),
                ["additionalProperties"] = JsonSerializer.SerializeToElement(false),
            };

            var response = await _client.Messages.Create(new MessageCreateParams
            {
                Model = Model.ClaudeOpus4_8,
                // Adaptive thinking plus up to 3 web searches can consume a
                // large share of the output budget before the model reaches
                // the final structured JSON - too tight a cap here doesn't
                // fail loudly, it truncates the JSON's string content into
                // garbled, half-written text that still happens to parse.
                MaxTokens = 8192,
                Thinking = new ThinkingConfigAdaptive(),
                OutputConfig = new OutputConfig
                {
                    Effort = Effort.High,
                    Format = new JsonOutputFormat { Schema = schema },
                },
                Tools = new List<ToolUnion>
                {
                    new ToolUnion(new WebSearchTool20260209 { MaxUses = 3 }),
                },
                Messages = new List<MessageParam>
                {
                    new()
                    {
                        Role = Role.User,
                        Content = new List<ContentBlockParam>
                        {
                            new ImageBlockParam
                            {
                                Source = new Base64ImageSource
                                {
                                    MediaType = "image/jpeg",
                                    Data = imageBase64,
                                },
                            },
                            new TextBlockParam { Text = BuildInstruction(photo, request) },
                        },
                    },
                },
            }, cancellationToken);

            if (response.StopReason == "refusal")
                throw new InvalidOperationException("The AI declined to analyze this photo.");

            // A max_tokens cutoff mid-generation can still leave syntactically
            // valid but semantically garbled JSON (truncated sentences,
            // stray draft fragments) - that must not silently reach the form.
            if (response.StopReason == "max_tokens")
                throw new InvalidOperationException("The AI response was cut off before it finished. Try a shorter length.");

            var jsonText = response.Content
                .Select(b => b.Value)
                .OfType<TextBlock>()
                .Select(b => b.Text)
                .LastOrDefault(t => !string.IsNullOrWhiteSpace(t));

            if (jsonText is null)
                throw new InvalidOperationException("The AI did not return a description.");

            var draft = JsonSerializer.Deserialize<GeneratedDraft>(
                jsonText,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
                ?? throw new InvalidOperationException("The AI response could not be parsed.");

            return new GenerateDescriptionResponse
            {
                Title = draft.Title,
                Description = draft.Description,
                SuggestedTags = draft.Tags,
            };
        }

        private static string BuildInstruction(Photo photo, GenerateDescriptionRequest request)
        {
            var style = request.Style.ToLowerInvariant() switch
            {
                "artistic" => "an evocative, artistic/illustrative style",
                "scientific" => "a precise, scientific/technical style",
                "journalistic" => "a journalistic, publicistic style",
                _ => "a clear, informative style",
            };

            var length = request.Length.ToLowerInvariant() switch
            {
                "short" => "very short (1-2 sentences)",
                "long" => "long and detailed (several paragraphs)",
                _ => "medium length (one short paragraph)",
            };

            var context = new List<string>();
            if (!string.IsNullOrWhiteSpace(photo.Title))
                context.Add($"Current title: {photo.Title}");
            if (photo.Latitude is not null && photo.Longitude is not null)
                context.Add($"GPS coordinates: {photo.Latitude}, {photo.Longitude}");
            if (!string.IsNullOrWhiteSpace(photo.ExifJson))
                context.Add($"EXIF metadata (JSON): {photo.ExifJson}");

            var contextBlock = context.Count > 0
                ? "\n\nExisting context about this photo:\n" + string.Join("\n", context)
                : "";

            var userInstructionsBlock = string.IsNullOrWhiteSpace(request.AdditionalInstructions)
                ? ""
                : $"\n\nAdditional instructions from the user - follow these, including any" +
                  $" requested output language, unless they conflict with accuracy:\n" +
                  $"{request.AdditionalInstructions.Trim()}";

            return $"""
                Identify the specific real-world subject of this photo (e.g. the exact
                ship, building, landmark, species, or event depicted - not just a generic
                description). Use web search to find genuinely interesting, accurate facts
                about that subject if you can identify it with reasonable confidence.

                Write a title and a description in {style}, {length}.
                Also suggest 3-6 short tag names (single words or short phrases) relevant
                to the subject.

                If you cannot confidently identify the specific subject, describe what is
                visibly depicted instead - do not invent facts.
                {contextBlock}
                {userInstructionsBlock}
                """;
        }

        private sealed class GeneratedDraft
        {
            public string Title { get; set; } = "";
            public string Description { get; set; } = "";
            public List<string> Tags { get; set; } = new();
        }
    }
}
