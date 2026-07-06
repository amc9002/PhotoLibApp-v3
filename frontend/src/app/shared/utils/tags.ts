/** Splits a comma-separated tag input into a trimmed, de-duplicated list. */
export function parseTagsInput(text: string): string[] {
  return Array.from(
    new Set(
      text
        .split(',')
        .map((t) => t.trim())
        .filter((t) => t.length > 0),
    ),
  );
}
