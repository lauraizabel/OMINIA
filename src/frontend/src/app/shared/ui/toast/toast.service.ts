import { Injectable, signal } from '@angular/core';

export interface ToastMessage {
  id: number;
  message: string;
  tone: 'success' | 'info';
}

@Injectable({ providedIn: 'root' })
export class ToastService {
  private nextId = 0;
  private readonly messagesState = signal<ToastMessage[]>([]);

  readonly messages = this.messagesState.asReadonly();

  success(message: string): void {
    this.show(message, 'success');
  }

  info(message: string): void {
    this.show(message, 'info');
  }

  dismiss(id: number): void {
    this.messagesState.update((messages) => messages.filter((message) => message.id !== id));
  }

  private show(message: string, tone: ToastMessage['tone']): void {
    const id = ++this.nextId;
    this.messagesState.update((messages) => [...messages, { id, message, tone }]);
    window.setTimeout(() => this.dismiss(id), 4_500);
  }
}
