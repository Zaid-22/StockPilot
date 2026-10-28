import { useEffect, useState } from 'react'
import { useQuery, useQueryClient } from '@tanstack/react-query'
import { useForm, useWatch } from 'react-hook-form'
import { zodResolver } from '@hookform/resolvers/zod'
import { Alert, Box, Button, Checkbox, Dialog, DialogActions, DialogContent, DialogTitle, FormControlLabel, Stack, TextField, Typography } from '@mui/material'
import { apiMutation, apiRequest } from '../../lib/api'
import { useIdempotentMutation } from '../../lib/useIdempotentMutation'
import { catalogs, defaults, productSchema, referenceSchema } from './catalog'

export default function CatalogEditor({ kind, id, categories, onClose, onSaved }) {
  const detail = useQuery({ queryKey: ['catalog', kind, id], queryFn: () => apiRequest(`/catalog/${kind}/${id}`), enabled: Boolean(id), staleTime: 0, retry: false })
  const [busy, setBusy] = useState(false)
  return <Dialog open onClose={() => { if (!busy) onClose() }} fullWidth maxWidth="sm" aria-labelledby="editor-title">
    <DialogTitle id="editor-title">{id ? 'Edit' : 'Add'} {catalogs[kind].singular}</DialogTitle>
    {id && detail.isPending ? <DialogContent><Typography role="status">Loading details…</Typography></DialogContent>
      : id && detail.error && !detail.data ? <><DialogContent><Alert severity="error" action={<Button onClick={() => detail.refetch()}>Retry</Button>}>{detail.error.message}</Alert></DialogContent><DialogActions><Button onClick={onClose}>Close</Button></DialogActions></>
        : <EditorForm key={id ?? 'new'} kind={kind} initialRecord={detail.data} categories={categories} onClose={onClose} onSaved={onSaved} setBusy={setBusy} reload={() => detail.refetch()} />}
  </Dialog>
}

function EditorForm({ kind, initialRecord, categories, onClose, onSaved, setBusy, reload }) {
  const product = kind === 'products'
  // Keep the values and concurrency version from when editing began together.
  // Only an explicit reload may replace this snapshot.
  const [record, setRecord] = useState(initialRecord)
  const [reloading, setReloading] = useState(false)
  const [editError, setError] = useState(null)
  const queryClient = useQueryClient()
  const { register, handleSubmit, control, reset, formState: { errors, isSubmitting } } = useForm({
    defaultValues: defaults(record, product), resolver: zodResolver(product ? productSchema : referenceSchema),
  })
  const categoryId = useWatch({ control, name: 'categoryId' })
  const isActive = useWatch({ control, name: 'isActive' })
  async function saved() {
    await queryClient.invalidateQueries({ queryKey: ['catalog'] })
    onSaved(`${catalogs[kind].singular[0].toUpperCase()}${catalogs[kind].singular.slice(1)} saved.`)
    onClose()
  }
  const creation = useIdempotentMutation({ mutationFn: payload => apiMutation(`/catalog/${kind}`, payload), onSuccess: saved })
  const error = editError || creation.error
  const locked = isSubmitting || creation.locked || reloading
  useEffect(() => { setBusy(locked) }, [locked, setBusy])
  async function reloadLatest() {
    setReloading(true)
    try {
      const result = await reload()
      if (result.error) throw result.error
      setRecord(result.data)
      reset(defaults(result.data, product))
      setError(null)
    } catch (failure) { setError(failure) }
    finally { setReloading(false) }
  }
  async function save(values) {
    const payload = { ...values, ...(record ? { version: record.version } : {}) }
    if (!product) for (const field of ['email', 'phone', 'address']) payload[field] = payload[field] || null
    if (!record) {
      creation.submit(() => ({ ...payload, requestId: crypto.randomUUID() }))
      return
    }
    setError(null)
    try {
      await apiMutation(`/catalog/${kind}/${record.id}`, payload, 'PUT')
      await saved()
    } catch (failure) { setError(failure) }
  }
  const field = (key, label, options = {}) => <TextField key={key} label={label} fullWidth {...register(key)} error={Boolean(errors[key])} helperText={errors[key]?.message} {...options} />
  return <form onSubmit={creation.uncertain ? event => { event.preventDefault(); creation.retry() } : handleSubmit(save)} noValidate>
    <DialogContent><Stack spacing={2.5} sx={{ pt: 1 }}>
      {error && <Alert severity="error" action={record ? <Button disabled={locked} onClick={reloadLatest}>{reloading ? 'Reloading…' : 'Reload latest'}</Button> : undefined}>{error.message}{creation.uncertain && ' Result unknown. Retry this request before making changes.'}</Alert>}
      <Box component="fieldset" disabled={locked} sx={{ border: 0, p: 0, m: 0, minWidth: 0 }}><Stack spacing={2.5}>
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
        <Typography variant="body2" color="text.secondary">New products start at zero stock. Use Inventory to adjust stock or receive a confirmed purchase order.</Typography>
      </> : kind !== 'categories' && <>{field('email', 'Email', { type: 'email' })}{field('phone', 'Phone')}{field('address', 'Address', { multiline: true, minRows: 2 })}</>}
      <FormControlLabel control={<Checkbox {...register('isActive')} checked={isActive} />} label="Active" />
      {record && <Typography variant="caption" color="text.secondary">Inactive records remain available in history. Reloading latest changes discards unsaved edits.</Typography>}
    </Stack></Box></Stack></DialogContent>
    <DialogActions sx={{ p: 3 }}><Button onClick={onClose} disabled={locked}>Cancel</Button><Button type="submit" variant="contained" disabled={isSubmitting || creation.isPending || reloading}>{creation.isPending || isSubmitting ? 'Saving…' : creation.uncertain ? 'Retry creation' : 'Save'}</Button></DialogActions>
  </form>
}
