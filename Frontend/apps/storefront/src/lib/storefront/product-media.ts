export function productImageSource(imageUrl: string | null | undefined): string | null {
  if (typeof imageUrl !== "string" || !imageUrl.trim()) return null;

  try {
    const url = new URL(imageUrl);
    if (url.protocol !== "http:" && url.protocol !== "https:") return null;

    // Portfolio media is served from Unsplash. Request one stable portrait crop for all card stages.
    if (url.hostname === "images.unsplash.com") {
      url.searchParams.set("auto", "format");
      url.searchParams.set("fit", "crop");
      url.searchParams.set("w", "1200");
      url.searchParams.set("h", "1500");
      url.searchParams.set("q", "85");
    }

    return url.toString();
  } catch {
    return null;
  }
}