import { FormControl, FormGroup } from '@angular/forms';

export type ItemForm = FormGroup<{
  id: FormControl<string>;
  productExternalId: FormControl<string>;
  productName: FormControl<string>;
  quantity: FormControl<number>;
  unitPrice: FormControl<number>;
}>;
