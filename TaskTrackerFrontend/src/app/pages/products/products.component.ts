import { ChangeDetectionStrategy, ChangeDetectorRef, Component, inject, OnInit } from '@angular/core';
import { CurrencyPipe } from '@angular/common';
import { ReactiveFormsModule, FormBuilder, Validators } from '@angular/forms';
import { finalize } from 'rxjs';
import { ProductService } from '../../services/product.service';
import { ProductDto, CreateUpdateProductDto } from '../../models';

@Component({
  selector: 'app-products',
  imports: [ReactiveFormsModule, CurrencyPipe],
  templateUrl: './products.component.html',
  styleUrl: './products.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class ProductsComponent implements OnInit {

  private svc = inject(ProductService);
  private fb  = inject(FormBuilder);
  private cdr = inject(ChangeDetectorRef);

  products: ProductDto[] = [];
  loading = true;
  showModal = false;
  editingId: string | null = null;
  submitting = false;
  error = '';

  form = this.fb.group({
    name:        ['', Validators.required],
    description: ['', Validators.required],
    price:       [0, [Validators.required, Validators.min(0)]],
  });

  ngOnInit() { this.load(); }

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
    this.form.reset({ price: 0 });
    this.error = '';
    this.showModal = true;
  }

  openEdit(p: ProductDto) {
    this.editingId = p.id;
    this.form.patchValue({ name: p.name, description: p.description, price: p.price });
    this.error = '';
    this.showModal = true;
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

  delete(id: string) {
    if (!confirm('Delete this product?')) return;
    this.svc.delete(id).subscribe({
      next: () => this.load()
      // no markForCheck needed here since load() handles it
    });
  }
}