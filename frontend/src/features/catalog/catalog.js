import { z } from 'zod'
import { apiRequest } from '../../lib/api'

export const catalogs = {
  products: { title: 'Products', singular: 'product', description: 'Keep products organized with a unique SKU, category, and reorder level.' },
  categories: { title: 'Categories', singular: 'category', description: 'Group products so your team can find them easily.' },
  suppliers: { title: 'Suppliers', singular: 'supplier', description: 'Maintain the people and companies you purchase from.' },
  customers: { title: 'Customers', singular: 'customer', description: 'Keep customer contact details ready for sales orders.' },
}
const name = z.string().trim().min(1, 'Enter a name.').max(160)
const optionalEmail = z.union([z.literal(''), z.string().trim().email('Enter a valid email.').max(256)])
export const referenceSchema = z.object({ name, email: optionalEmail, phone: z.string().trim().max(40), address: z.string().trim().max(500), isActive: z.boolean() })
export const productSchema = z.object({
  name, sku: z.string().trim().toUpperCase().regex(/^[A-Z0-9][A-Z0-9._-]{0,63}$/, 'Use 1–64 letters, numbers, dots, underscores or hyphens.'),
  description: z.string().trim().max(2000), categoryId: z.string().uuid('Choose a category.'),
  sellingPrice: z.string().regex(/^\d{1,9}(\.\d{1,3})?$/, 'Enter a non-negative price with up to 3 decimal places.').transform(Number),
  reorderLevel: z.string().regex(/^\d+$/, 'Enter a whole number.').transform(Number).pipe(z.number().int().min(0).max(1000000000)),
  isActive: z.boolean(),
})

export async function categoryOptions() {
  const items = []
  let page = 1
  while (true) {
    const result = await apiRequest(`/catalog/categories?page=${page}&pageSize=100`)
    items.push(...result.items)
    if (items.length >= result.totalCount || result.items.length === 0) return items
    page += 1
  }
}

export function defaults(record, product) {
  return product ? {
    name: record?.name ?? '', sku: record?.sku ?? '', description: record?.description ?? '',
    categoryId: record?.categoryId ?? '', sellingPrice: String(record?.sellingPrice ?? 0),
    reorderLevel: String(record?.reorderLevel ?? 0), isActive: record?.isActive ?? true,
  } : { name: record?.name ?? '', email: record?.email ?? '', phone: record?.phone ?? '', address: record?.address ?? '', isActive: record?.isActive ?? true }
}
