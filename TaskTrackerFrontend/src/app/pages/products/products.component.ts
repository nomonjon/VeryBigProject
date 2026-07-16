import { ChangeDetectionStrategy, ChangeDetectorRef, Component, inject, OnInit } from '@angular/core';
import { CurrencyPipe } from '@angular/common';
import { ReactiveFormsModule, FormBuilder, Validators } from '@angular/forms';
import { finalize } from 'rxjs';
import { ProductService } from '../../services/product.service';
import { RuleService } from '../../services/rule.service';
import { ProductDto, CreateUpdateProductDto, ProductRuleDto, CreateUpdateProductRuleDto } from '../../models';

@Component({
  selector: 'app-products',
  imports: [ReactiveFormsModule, CurrencyPipe],
  templateUrl: './products.component.html',
  styleUrl: './products.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class ProductsComponent implements OnInit {

  private svc = inject(ProductService);
  private ruleSvc = inject(RuleService);
  private fb  = inject(FormBuilder);
  private cdr = inject(ChangeDetectorRef);

  products: ProductDto[] = [];
  loading = true;
  showModal = false;
  editingId: string | null = null;
  submitting = false;
  error = '';

  // ── Rules ──
  rules: ProductRuleDto[] = [];
  rulesLoading = true;
  showRuleModal = false;
  ruleSubmitting = false;
  ruleError = '';

  form = this.fb.group({
    name:     ['', Validators.required],
    quantity: [0, [Validators.required, Validators.min(0)]],
    price:    [0, [Validators.required, Validators.min(0)]],
  });

  ruleForm = this.fb.group({
    name:       ['', Validators.required],
    expression: ['', Validators.required],
    color:      ['orange', Validators.required],
  });

  ngOnInit() {
    this.load();
    this.loadRules();
  }

  load() {
    this.loading = true;
    this.svc.getAll().pipe(
      finalize(() => {
        this.loading = false;
        this.cdr.markForCheck(); // 👈 finalize runs outside zone
      })
    ).subscribe({
      next: (d) => {
        this.products = d;
        this.cdr.markForCheck(); // 👈
      },
      error: (err) => {
        console.error('Products load error', err);
        this.cdr.markForCheck(); // 👈
      },
    });
  }

  openCreate() {
    this.editingId = null;
    this.form.reset({ quantity: 0, price: 0 });
    this.error = '';
    this.showModal = true;
  }

  openEdit(p: ProductDto) {
    this.editingId = p.id;
    this.form.patchValue({ name: p.name, quantity: p.quantity, price: p.price });
    this.error = '';
    this.showModal = true;
  }

  // Inline quick stock adjust from the product row (no modal). Sends the whole
  // product because the update endpoint replaces name/quantity/price.
  adjustQty(p: ProductDto, delta: number) {
    const quantity = Math.max(0, p.quantity + delta);
    if (quantity === p.quantity) return;
    const dto: CreateUpdateProductDto = { name: p.name, quantity, price: p.price };
    this.svc.update(p.id, dto).subscribe({
      next: () => this.load(),
      error: (err) => {
        console.error('Quantity update error', err);
        this.cdr.markForCheck();
      },
    });
  }

  closeModal() {
    this.showModal = false;
  }

  submit() {
    if (this.form.invalid) return;
    this.submitting = true;
    const dto = this.form.value as CreateUpdateProductDto;
    const req$ = this.editingId ? this.svc.update(this.editingId, dto) : this.svc.create(dto);
    req$.pipe(
      finalize(() => {
        this.submitting = false;
        this.cdr.markForCheck(); // 👈
      })
    ).subscribe({
      next: () => {
        this.showModal = false;
        this.load();
      },
      error: (err) => {
        this.error = err.error?.message || 'Error saving product';
        this.cdr.markForCheck(); // 👈
      },
    });
  }

  // Human label for a product's rule-driven color.
  statusLabel(color: string): string {
    switch (color) {
      case 'red':    return 'Out of stock';
      case 'orange': return 'Low stock';
      default:       return 'In stock';
    }
  }

  delete(id: string) {
    if (!confirm('Delete this product?')) return;
    this.svc.delete(id).subscribe({
      next: () => this.load()
      // no markForCheck needed here since load() handles it
    });
  }

  // ── Rules ──────────────────────────────────────────────────────────────────

  loadRules() {
    this.rulesLoading = true;
    this.ruleSvc.getAll().pipe(
      finalize(() => {
        this.rulesLoading = false;
        this.cdr.markForCheck();
      })
    ).subscribe({
      next: (r) => {
        this.rules = r;
        this.cdr.markForCheck();
      },
      error: (err) => {
        console.error('Rules load error', err);
        this.cdr.markForCheck();
      },
    });
  }

  openCreateRule() {
    this.ruleForm.reset({ name: '', expression: '', color: 'orange' });
    this.ruleError = '';
    this.showRuleModal = true;
  }

  closeRuleModal() {
    this.showRuleModal = false;
  }

  submitRule() {
    if (this.ruleForm.invalid) return;
    this.ruleSubmitting = true;
    const dto: CreateUpdateProductRuleDto = {
      name: this.ruleForm.value.name!,
      expression: this.ruleForm.value.expression!,
      color: this.ruleForm.value.color!,
      isActive: true,
    };
    this.ruleSvc.create(dto).pipe(
      finalize(() => {
        this.ruleSubmitting = false;
        this.cdr.markForCheck();
      })
    ).subscribe({
      next: () => {
        this.showRuleModal = false;
        this.loadRules();
        // Products get re-colored by the server sweep (~15s); refresh to show it.
        this.load();
      },
      error: (err) => {
        this.ruleError = (typeof err.error === 'string' ? err.error : err.error?.message)
          || 'Invalid rule expression';
        this.cdr.markForCheck();
      },
    });
  }

  deleteRule(id: string) {
    if (!confirm('Delete this rule?')) return;
    this.ruleSvc.delete(id).subscribe({
      next: () => {
        this.loadRules();
        this.load();
      },
    });
  }
}