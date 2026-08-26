const productImages: Record<string, string> = {
  "Aurora Wireless Headphones": "/images/products/aurora-wireless-headphones.png",
  "Orbit Mechanical Keyboard": "/images/products/orbit-mechanical-keyboard.png",
  "Field Notes Desk Set": "/images/products/field-notes-desk-set.png",
  "Atlas USB-C Hub": "/images/products/atlas-usb-c-hub.png",
};

export function productImageSource(productName: string): string | null {
  return productImages[productName] ?? null;
}
