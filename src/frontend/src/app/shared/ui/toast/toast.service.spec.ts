import { ToastService } from './toast.service';

describe('ToastService', () => {
  afterEach(() => vi.useRealTimers());

  it('publishes and automatically dismisses a success message', () => {
    vi.useFakeTimers();
    const service = new ToastService();

    service.success('Sale created successfully.');
    expect(service.messages()).toEqual([
      expect.objectContaining({ message: 'Sale created successfully.', tone: 'success' }),
    ]);

    vi.advanceTimersByTime(4_500);
    expect(service.messages()).toEqual([]);
  });
});
