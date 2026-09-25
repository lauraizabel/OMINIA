import { PercentPipe } from '@angular/common';
import { Component, ElementRef, inject, input, output } from '@angular/core';
import { AbstractControl, FormArray, ReactiveFormsModule } from '@angular/forms';
import { ButtonDirective } from '../../../../shared/ui/button/button.directive';
import { Icon } from '../../../../shared/ui/icon/icon';
import { MoneyPipe } from '../../../../shared/ui/money.pipe';
import { previewLine } from '../../shared/sale-money';
import { ItemForm } from '../sale-form.types';

@Component({
  selector: 'app-sale-items-editor',
  imports: [ButtonDirective, Icon, MoneyPipe, PercentPipe, ReactiveFormsModule],
  templateUrl: './sale-items-editor.html',
  styleUrl: './sale-items-editor.scss',
})
export class SaleItemsEditor {
  private readonly host: ElementRef<HTMLElement> = inject(ElementRef);

  readonly items = input.required<FormArray<ItemForm>>();
  readonly errorFor = input.required<(control: AbstractControl, field: string) => string>();
  readonly itemAdded = output<void>();
  readonly itemRemoved = output<number>();

  addItem(): void {
    if (this.items().length >= 100) return;
    const index = this.items().length;
    this.itemAdded.emit();
    setTimeout(() =>
      this.host.nativeElement
        .querySelector<HTMLInputElement>(`[data-item-index="${index}"] input`)
        ?.focus(),
    );
  }

  preview(index: number) {
    const item = this.items().at(index).getRawValue();
    return previewLine(Number(item.quantity), Number(item.unitPrice));
  }

  grossTotal(): number {
    return this.items().controls.reduce(
      (total, _, index) => total + this.preview(index).grossAmount,
      0,
    );
  }

  discountTotal(): number {
    return this.items().controls.reduce(
      (total, _, index) => total + this.preview(index).discountAmount,
      0,
    );
  }

  total(): number {
    return this.items().controls.reduce(
      (total, _, index) => total + this.preview(index).totalAmount,
      0,
    );
  }
}
