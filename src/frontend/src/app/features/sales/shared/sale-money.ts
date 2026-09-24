export interface LinePreview {
  grossAmount: number;
  discountRate: number;
  discountAmount: number;
  totalAmount: number;
}

export function previewLine(quantity: number, unitPrice: number): LinePreview {
  const priceInCents = Math.round((Number.isFinite(unitPrice) ? unitPrice : 0) * 100);
  const safeQuantity = Number.isInteger(quantity) ? quantity : 0;
  const grossInCents = priceInCents * safeQuantity;
  const discountPercent = safeQuantity >= 10 ? 20 : safeQuantity >= 4 ? 10 : 0;
  const discountInCents = Math.round((grossInCents * discountPercent) / 100);
  return {
    grossAmount: grossInCents / 100,
    discountRate: discountPercent / 100,
    discountAmount: discountInCents / 100,
    totalAmount: (grossInCents - discountInCents) / 100,
  };
}
