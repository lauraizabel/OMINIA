import { DatePipe } from '@angular/common';
import { Component, ElementRef, inject, signal } from '@angular/core';
import {
  AbstractControl,
  FormArray,
  FormControl,
  FormGroup,
  ReactiveFormsModule,
  ValidationErrors,
  Validators,
} from '@angular/forms';
import { ActivatedRoute, Router } from '@angular/router';
import { finalize } from 'rxjs';
import { safeReturnUrl } from '../../../core/auth/return-url';
import { Alert } from '../../../shared/ui/alert/alert';
import { ButtonDirective } from '../../../shared/ui/button/button.directive';
import { Icon } from '../../../shared/ui/icon/icon';
import { MoneyPipe } from '../../../shared/ui/money.pipe';
import { PageHeader } from '../../../shared/ui/page-header/page-header';
import { ToastService } from '../../../shared/ui/toast/toast.service';
import { SalesApiService } from '../data-access/sales-api.service';
import {
  CreateSaleRequest,
  SaleItem,
  SaleItemInput,
  SaleResource,
  UpdateSaleRequest,
} from '../data-access/sales.models';
import { mapSaleError } from '../shared/sale-errors';
import { PendingChangesAware } from '../shared/pending-changes.guard';
import { ItemForm } from './sale-form.types';
import { SaleItemsEditor } from './sale-items-editor/sale-items-editor';

@Component({
  selector: 'app-sale-editor',
  imports: [
    Alert,
    ButtonDirective,
    DatePipe,
    Icon,
    MoneyPipe,
    PageHeader,
    ReactiveFormsModule,
    SaleItemsEditor,
  ],
  templateUrl: './sale-editor.html',
  styleUrl: './sale-editor.scss',
})
export class SaleEditor implements PendingChangesAware {
  private readonly api = inject(SalesApiService);
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly host: ElementRef<HTMLElement> = inject(ElementRef);
  private readonly toast = inject(ToastService);

  readonly saleId = this.route.snapshot.paramMap.get('id');
  readonly isEdit = this.saleId !== null;
  readonly returnUrl = safeReturnUrl(this.route.snapshot.queryParamMap.get('returnUrl'));
  readonly loading = signal(this.isEdit);
  readonly saving = signal(false);
  readonly loadError = signal('');
  readonly submitError = signal('');
  readonly conflict = signal(false);
  readonly serverFields = signal<Record<string, string>>({});
  readonly cancelledItems = signal<SaleItem[]>([]);
  readonly saleNumber = signal('');
  private etag = '';
  private saved = false;

  readonly items = new FormArray<ItemForm>([], {
    validators: [duplicateProductValidator, Validators.minLength(1), Validators.maxLength(100)],
  });
  readonly form = new FormGroup({
    saleNumber: new FormControl('', {
      nonNullable: true,
      validators: [Validators.required, Validators.maxLength(50), notBlank],
    }),
    saleDate: new FormControl('', { nonNullable: true, validators: [Validators.required] }),
    customerExternalId: identityIdControl(),
    customerName: identityNameControl(),
    branchExternalId: identityIdControl(),
    branchName: identityNameControl(),
    items: this.items,
  });

  constructor() {
    if (this.isEdit) {
      this.form.controls.saleNumber.disable();
      this.loadSale();
    } else {
      this.form.controls.saleDate.setValue(toLocalDateTime(new Date().toISOString()));
      this.addItem();
    }
  }

  hasPendingChanges(): boolean {
    return this.form.dirty && !this.saved;
  }

  cancel(): void {
    if (this.isEdit) {
      void this.router.navigate(['/sales', this.saleId], {
        queryParams: { returnUrl: this.returnUrl },
      });
      return;
    }
    void this.router.navigateByUrl(this.returnUrl);
  }

  addItem(): void {
    if (this.items.length >= 100) return;
    this.items.push(createItemForm());
    this.form.markAsDirty();
  }

  removeItem(index: number): void {
    if (this.items.length <= 1) return;
    this.items.removeAt(index);
    this.form.markAsDirty();
  }

  readonly fieldError = (control: AbstractControl, field: string): string =>
    this.errorFor(control, field);

  errorFor(control: AbstractControl, field: string): string {
    const serverError = this.serverFields()[field] ?? this.serverFields()[capitalize(field)];
    if (serverError) return serverError;
    if (!control.touched || !control.errors) return '';
    if (control.hasError('required')) return 'This field is required.';
    if (control.hasError('maxlength'))
      return `Maximum length is ${control.getError('maxlength').requiredLength}.`;
    if (control.hasError('min')) return `Minimum value is ${control.getError('min').min}.`;
    if (control.hasError('max')) return `Maximum value is ${control.getError('max').max}.`;
    if (control.hasError('pattern')) return 'Use a positive amount with up to two decimal places.';
    if (control.hasError('blank')) return 'This field cannot contain only spaces.';
    if (control.hasError('controlCharacters')) return 'Control characters are not allowed.';
    return 'Review this value.';
  }

  submit(): void {
    if (this.saving()) return;
    this.serverFields.set({});
    this.submitError.set('');
    this.conflict.set(false);
    this.form.markAllAsTouched();

    if (this.form.invalid) {
      this.submitError.set(
        this.items.hasError('duplicateProduct')
          ? 'Each product ID can appear only once in a sale.'
          : this.items.hasError('minlength')
            ? 'A sale needs at least one active item.'
            : 'Review the highlighted fields before saving.',
      );
      this.focusFirstInvalid();
      return;
    }

    const value = this.form.getRawValue();
    const common = {
      saleDate: new Date(value.saleDate).toISOString(),
      customer: { externalId: value.customerExternalId.trim(), name: value.customerName.trim() },
      branch: { externalId: value.branchExternalId.trim(), name: value.branchName.trim() },
      items: value.items.map((item): SaleItemInput => ({
        ...(item.id ? { id: item.id } : {}),
        product: {
          externalId: item.productExternalId.trim(),
          name: item.productName.trim(),
        },
        quantity: Number(item.quantity),
        unitPrice: Number(item.unitPrice),
      })),
    };

    this.saving.set(true);
    const request = this.isEdit
      ? this.api.update(this.saleId!, common as UpdateSaleRequest, this.etag)
      : this.api.create({ ...common, saleNumber: value.saleNumber.trim() } as CreateSaleRequest);

    request.pipe(finalize(() => this.saving.set(false))).subscribe({
      next: (resource) => {
        this.saved = true;
        this.toast.success(
          this.isEdit ? 'Sale updated successfully.' : 'Sale created successfully.',
        );
        void this.router.navigate(['/sales', resource.sale.id], {
          queryParams: { returnUrl: this.returnUrl },
        });
      },
      error: (error: unknown) => {
        const state = mapSaleError(error);
        this.submitError.set(state.message);
        this.serverFields.set(state.fields);
        this.conflict.set(state.conflict);
        this.focusFirstInvalid();
      },
    });
  }

  reloadCurrent(): void {
    if (!this.isEdit) return;
    if (
      this.form.dirty &&
      !window.confirm('Reload the current server version and discard this draft?')
    )
      return;
    this.loadSale();
  }

  private loadSale(): void {
    this.loading.set(true);
    this.loadError.set('');
    this.api
      .get(this.saleId!)
      .pipe(finalize(() => this.loading.set(false)))
      .subscribe({
        next: (resource) => this.populate(resource),
        error: (error: unknown) => this.loadError.set(mapSaleError(error).message),
      });
  }

  private populate(resource: SaleResource): void {
    const sale = resource.sale;
    this.etag = resource.etag;
    this.saleNumber.set(sale.saleNumber);
    this.cancelledItems.set(sale.items.filter((item) => item.isCancelled));
    this.items.clear();
    for (const item of sale.items.filter((candidate) => !candidate.isCancelled)) {
      this.items.push(createItemForm(item));
    }
    this.form.patchValue({
      saleNumber: sale.saleNumber,
      saleDate: toLocalDateTime(sale.saleDate),
      customerExternalId: sale.customer.externalId,
      customerName: sale.customer.name,
      branchExternalId: sale.branch.externalId,
      branchName: sale.branch.name,
    });
    this.form.markAsPristine();
    this.form.markAsUntouched();
    this.submitError.set('');
    this.serverFields.set({});
    this.conflict.set(false);
  }

  private focusFirstInvalid(): void {
    setTimeout(() =>
      this.host.nativeElement
        .querySelector<HTMLElement>('input.ng-invalid, select.ng-invalid, [role="alert"]')
        ?.focus(),
    );
  }
}

function createItemForm(item?: SaleItem): ItemForm {
  const form = new FormGroup({
    id: new FormControl(item?.id ?? '', { nonNullable: true }),
    productExternalId: identityIdControl(item?.product.externalId ?? ''),
    productName: identityNameControl(item?.product.name ?? ''),
    quantity: new FormControl(item?.quantity ?? 1, {
      nonNullable: true,
      validators: [Validators.required, Validators.min(1), Validators.max(20)],
    }),
    unitPrice: new FormControl(item?.unitPrice ?? 0.01, {
      nonNullable: true,
      validators: [
        Validators.required,
        Validators.min(0.01),
        Validators.max(1_000_000),
        Validators.pattern(/^\d+(\.\d{1,2})?$/),
      ],
    }),
  });
  if (item) {
    form.controls.productExternalId.disable();
    form.controls.productName.disable();
  }
  return form;
}

function identityIdControl(value = ''): FormControl<string> {
  return new FormControl(value, {
    nonNullable: true,
    validators: [Validators.required, Validators.maxLength(100), notBlank, noControlCharacters],
  });
}

function identityNameControl(value = ''): FormControl<string> {
  return new FormControl(value, {
    nonNullable: true,
    validators: [Validators.required, Validators.maxLength(200), notBlank],
  });
}

function noControlCharacters(control: AbstractControl<string>): ValidationErrors | null {
  const hasControlCharacter = [...control.value].some((character) => {
    const code = character.charCodeAt(0);
    return code <= 31 || code === 127;
  });
  return hasControlCharacter ? { controlCharacters: true } : null;
}

function notBlank(control: AbstractControl<string>): ValidationErrors | null {
  return control.value.length > 0 && control.value.trim().length === 0 ? { blank: true } : null;
}

function duplicateProductValidator(control: AbstractControl): ValidationErrors | null {
  const values = (control as FormArray<ItemForm>)
    .getRawValue()
    .map((item) => item.productExternalId?.trim())
    .filter(Boolean);
  return new Set(values).size === values.length ? null : { duplicateProduct: true };
}

function toLocalDateTime(iso: string): string {
  const date = new Date(iso);
  const local = new Date(date.getTime() - date.getTimezoneOffset() * 60_000);
  return local.toISOString().slice(0, 16);
}

function capitalize(value: string): string {
  return value.charAt(0).toUpperCase() + value.slice(1);
}
