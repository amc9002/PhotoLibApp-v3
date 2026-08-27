/** Single-letter fallback shown in an avatar circle when no avatar image is set. */
export function userInitials(name: string | null | undefined): string {
  const trimmed = (name ?? '').trim();
  return trimmed ? trimmed.charAt(0).toUpperCase() : '?';
}
