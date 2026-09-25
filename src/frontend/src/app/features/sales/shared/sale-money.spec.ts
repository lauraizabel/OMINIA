import { previewLine } from './sale-money';

describe('previewLine', () => {
  it.each([
    [3, 0, 30],
    [4, 0.1, 36],
    [9, 0.1, 81],
    [10, 0.2, 80],
    [20, 0.2, 160],
  ])('previews quantity %i with the expected tier', (quantity, rate, total) => {
    expect(previewLine(quantity, 10)).toEqual({
      grossAmount: quantity * 10,
      discountRate: rate,
      discountAmount: quantity * 10 * rate,
      totalAmount: total,
    });
  });

  it('rounds the discount once per line in cents', () => {
    expect(previewLine(4, 0.03)).toEqual({
      grossAmount: 0.12,
      discountRate: 0.1,
      discountAmount: 0.01,
      totalAmount: 0.11,
    });
  });
});
