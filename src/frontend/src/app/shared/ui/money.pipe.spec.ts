import { formatMoney } from './money.pipe';

describe('formatMoney', () => {
  it('uses the Brazilian currency convention consistently', () => {
    expect(formatMoney(1234.5).replace(/\u00a0/g, ' ')).toBe('R$ 1.234,50');
  });

  it('uses zero for missing or invalid values', () => {
    expect(formatMoney(undefined).replace(/\u00a0/g, ' ')).toBe('R$ 0,00');
    expect(formatMoney(Number.NaN).replace(/\u00a0/g, ' ')).toBe('R$ 0,00');
  });
});
