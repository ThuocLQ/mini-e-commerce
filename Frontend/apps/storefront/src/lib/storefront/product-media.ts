export function productImageSource(imageUrl: string | null | undefined): string | null {
  if (typeof imageUrl !== "string" || !imageUrl.trim()) return null;

  try {
    const url = new URL(imageUrl);
    return url.protocol === "http:" || url.protocol === "https:" ? url.toString() : null;
  } catch {
    return null;
  }
}