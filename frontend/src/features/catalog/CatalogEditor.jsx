import { useState } from 'react'
import { useQuery, useQueryClient } from '@tanstack/react-query'
import { useForm, useWatch } from 'react-hook-form'
import { zodResolver } from '@hookform/resolvers/zod'
import { Alert, Button, Checkbox, Dialog, DialogActions, DialogContent, DialogTitle, FormControlLabel, Stack, TextField, Typography } from '@mui/material'
import { apiMutation, apiRequest } from '../../lib/api'
import { catalogs, defaults, productSchema, referenceSchema } from './catalog'

export default function CatalogEditor({ kind, id, categories, onClose, onSaved }) {
  const detail = useQuery({ queryKey: ['catalog', kind, id], queryFn: () => apiRequest(`/catalog/${kind}/${id}`), enabled: Boolean(id), staleTime: 0, retry: false })
  const [busy, setBusy] = useState(false)
  return <Dialog open onClose={() => { if (!busy) onClose() }} fullWidth maxWidth="sm" aria-labelledby="editor-title">
    <DialogTitle id="editor-title">{id ? 'Edit' : 'Add'} {catalogs[kind].singular}</DialogTitle>
    {id && detail.isPending ? <DialogContent><Typography role="status">Loading details…</Typography></DialogContent>
      : id && detail.error ? <><DialogContent><Alert severity="error" action={<Button onClick={() => detail.refetch()}>Retry</Button>}>{detail.error.message}</Alert></DialogContent><DialogActions><Button onClick={onClose}>Close</Button></DialogActions></>
        : <EditorForm key={detail.data?.version ?? 'new'} kind={kind} record={detail.data} categories={categories} onClose={onClose} onSaved={onSaved} setBusy={setBusy} reload={() => detail.refetch()} />}
  </Dialog>
}

function EditorForm({ kind, record, categories, onClose, onSaved, setBusy, reload }) {
  const product = kind === 'products'
  const [error, setError] = useState(null)
  const queryClient = useQueryClient()
  const { register, handleSubmit, control, formState: { errors, isSubmitting } } = useForm({
    defaultValues: defaults(record, product), resolver: zodResolver(product ? productSchema : referenceSchema),
  })
  const categoryId = useWatch({ control, name: 'categoryId' })
  const isActive = useWatch({ control, name: 'isActive' })
  async function save(values) {
    setBusy(true); setError(null)
    try {
      const payload = { ...values, ...(record ? { version: record.version } : {}) }
      if (!product) for (const field of ['email', 'phone', 'address']) payload[field] = payload[field] || null
      await apiMutation(`/catalog/${kind}${record ? `/${record.id}` : ''}`, payload, record ? 'PUT' : 'POST')
      await queryClient.invalidateQueries({ queryKey: ['catalog'] })
      onSaved(`${catalogs[kind].singular[0].toUpperCase()}${catalogs[kind].singular.slice(1)} saved.`)
      onClose()
    } catch (failure) { setError(failure) }
    finally { setBusy(false) }
  }
  const field = (key, label, options = {}) => <TextField key={key} label={label} fullWidth {...register(key)} error={Boolean(errors[key])} helperText={errors[key]?.message} {...options} />
  return <form onSubmit={handleSubmit(save)} noValidate>
    <DialogContent><Stack spacing={2.5} sx={{ pt: 1 }}>
      {error && <Alert severity="error" action={error.status === 409 && record ? <Button onClick={reload}>Reload latest</Button> : undefined}>{error.message}</Alert>}
      {field('name', 'Name', { autoFocus: true, required: true })}
      {product ? <>
        {field('sku', 'SKU', { required: true })}
        <TextField select label="Category" fullWidth required {...register('categoryId')} value={categoryId} slotProps={{ select: { native: true } }} error={Boolean(errors.categoryId)} helperText={errors.categoryId?.message}>
          <option value="">Choose a category</option>{categories.filter(c => c.isActive || c.id === record?.categoryId).map(c => <option key={c.id} value={c.id}>{c.name}{!c.isActive ? ' (inactive)' : ''}</option>)}
        </TextField>
        {categories.filter(c => c.isActive).length === 0 && !record && <Alert severity="info">Add an active category before creating a product.</Alert>}
        {field('sellingPrice', 'Selling price', { required: true, slotProps: { htmlInput: { inputMode: 'decimal' } } })}
        {field('reorderLevel', 'Reorder level', { required: true, slotProps: { htmlInput: { inputMode: 'numeric' } } })}
        {field('description', 'Description', { multiline: true, minRows: 3 })}
        <Typography variant="body2" color="text.secondary">New products start at zero stock. Stock adjustments and receiving will be added in the inventory phase.</Typography>
      </> : kind !== 'categories' && <>{field('email', 'Email', { type: 'email' })}{field('phone', 'Phone')}{field('address', 'Address', { multiline: true, minRows: 2 })}</>}
      <FormControlLabel control={<Checkbox {...register('isActive')} checked={isActive} />} label="Active" />
      {record && <Typography variant="caption" color="text.secondary">Inactive records remain available in history. Reloading latest changes discards unsaved edits.</Typography>}
    </Stack></DialogContent>
    <DialogActions sx={{ p: 3 }}><Button onClick={onClose} disabled={isSubmitting}>Cancel</Button><Button type="submit" variant="contained" disabled={isSubmitting}>{isSubmitting ? 'Saving…' : 'Save'}</Button></DialogActions>
  </form>
}
