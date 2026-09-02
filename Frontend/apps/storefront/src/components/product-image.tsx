"use client";

import { ImageOff } from "lucide-react";
import { useState } from "react";
import { productImageSource } from "@/lib/storefront/product-media";

type ProductImageProps = {
  alt: string;
  imageUrl: string | null | undefined;
  className: string;
  fallbackClassName: string;
  loading?: "eager" | "lazy";
  fetchPriority?: "high" | "auto";
};

export function ProductImage({
  alt,
  imageUrl,
  className,
  fallbackClassName,
  loading = "lazy",
  fetchPriority = "auto",
}: ProductImageProps) {
  const [hasFailed, setHasFailed] = useState(false);
  const source = productImageSource(imageUrl);

  if (!source || hasFailed) {
    return (
      <span aria-label={`${alt} image unavailable`} className={fallbackClassName} data-testid="product-image-fallback" role="img">
        <ImageOff aria-hidden="true" size={42} strokeWidth={1.25} />
      </span>
    );
  }

  return <img alt={alt} className={className} fetchPriority={fetchPriority} loading={loading} onError={() => setHasFailed(true)} referrerPolicy="no-referrer" src={source} />;
}