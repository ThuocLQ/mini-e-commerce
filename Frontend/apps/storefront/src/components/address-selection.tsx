"use client";

import { Check, ChevronDown, LoaderCircle, MapPin, Pencil, Plus, RefreshCw, Trash2 } from "lucide-react";
import { useState } from "react";
import type { AddressInput, CustomerAddress } from "@/lib/storefront/types";

export type AddressLoadState = "idle" | "loading" | "ready" | "unavailable";

type AddressSelectionProps = {
  addresses: CustomerAddress[];
  loadState: AddressLoadState;
  message: string | null;
  selectedAddressId: string | null;
  busyAddressId: string | null;
  onSelect: (addressId: string) => void;
  onCreate: (input: AddressInput) => void;
  onUpdate: (addressId: string, input: AddressInput) => void;
  onDelete: (addressId: string) => void;
  onSetDefault: (addressId: string) => void;
  onRetry: () => void;
};

const emptyAddress: AddressInput = { label: "", recipientName: "", line1: "", line2: null, city: "", countryCode: "", postalCode: null, makeDefault: false };

export function AddressSelection(props: AddressSelectionProps) {
  const [isFormOpen, setIsFormOpen] = useState(false);
  const [editingAddress, setEditingAddress] = useState<CustomerAddress | null>(null);
  const [confirmDeleteId, setConfirmDeleteId] = useState<string | null>(null);
  const { addresses, loadState, message, selectedAddressId, busyAddressId, onCreate, onDelete, onRetry, onSelect, onSetDefault, onUpdate } = props;

  function openCreate() {
    setEditingAddress(null);
    setIsFormOpen(true);
  }

  function openEdit(address: CustomerAddress) {
    setEditingAddress(address);
    setIsFormOpen(true);
  }

  function closeForm() {
    if (busyAddressId) return;
    setIsFormOpen(false);
    setEditingAddress(null);
  }

  return <section aria-labelledby="delivery-address-heading" className="mt-6 border-t border-[var(--line)] pt-5">
    <div className="flex items-start justify-between gap-4">
      <div><h3 className="font-semibold" id="delivery-address-heading">Delivery address</h3><p className="mt-1 text-sm leading-5 text-[var(--muted)]">Choose where this order should be addressed.</p></div>
      {loadState === "ready" && !isFormOpen ? <button className="inline-flex h-9 shrink-0 items-center gap-2 border border-[var(--accent)] px-3 text-sm font-semibold text-[var(--accent)] hover:bg-[#e9f2ed]" onClick={openCreate} type="button"><Plus aria-hidden="true" size={16} />Add</button> : null}
    </div>

    {message ? <p className="mt-4 border-l-2 border-[var(--danger)] bg-[#fff7f6] px-3 py-2 text-sm text-[var(--danger)]" role="alert">{message}</p> : null}
    {loadState === "loading" ? <div aria-live="polite" className="mt-5 flex items-center gap-2 text-sm text-[var(--muted)]"><LoaderCircle aria-hidden="true" className="animate-spin" size={17} />Loading saved addresses…</div> : null}
    {loadState === "unavailable" ? <div className="mt-5 border border-[#f3c5c1] bg-[#fff7f6] p-4"><p className="text-sm text-[var(--danger)]">Saved addresses could not be loaded. Your cart has not changed.</p><button className="mt-3 inline-flex h-9 items-center gap-2 border border-[var(--danger)] px-3 text-sm font-semibold text-[var(--danger)] hover:bg-white" onClick={onRetry} type="button"><RefreshCw aria-hidden="true" size={15} />Try again</button></div> : null}

    {loadState === "ready" && addresses.length > 0 ? <fieldset className="mt-5 space-y-3"><legend className="sr-only">Select a delivery address</legend>{addresses.map((address) => <AddressOption address={address} busyAddressId={busyAddressId} confirmDeleteId={confirmDeleteId} key={address.id} onCancelDelete={() => setConfirmDeleteId(null)} onDelete={() => onDelete(address.id)} onEdit={() => openEdit(address)} onRequestDelete={() => setConfirmDeleteId(address.id)} onSelect={() => onSelect(address.id)} onSetDefault={() => onSetDefault(address.id)} selected={address.id === selectedAddressId} />)}</fieldset> : null}
    {loadState === "ready" && addresses.length === 0 && !isFormOpen ? <div className="mt-5 border border-dashed border-[var(--line)] bg-white p-5 text-center"><MapPin aria-hidden="true" className="mx-auto text-[var(--muted)]" size={25} /><p className="mt-3 font-medium">Add a delivery address to continue</p><p className="mt-1 text-sm leading-5 text-[var(--muted)]">An address is required for this checkout.</p><button className="mt-4 inline-flex h-10 items-center gap-2 bg-[var(--accent)] px-4 text-sm font-semibold text-white hover:bg-[var(--accent-strong)]" onClick={openCreate} type="button"><Plus aria-hidden="true" size={16} />Add address</button></div> : null}
    {isFormOpen ? <AddressForm address={editingAddress} busy={busyAddressId === "new" || busyAddressId === editingAddress?.id} onCancel={closeForm} onSubmit={(input) => editingAddress ? onUpdate(editingAddress.id, input) : onCreate(input)} /> : null}
    {loadState === "ready" && selectedAddressId ? <p className="mt-4 text-xs leading-5 text-[var(--muted)]">When an order is confirmed, it keeps a snapshot of the selected address. This does not create shipping or fulfillment.</p> : null}
  </section>;
}

function AddressOption({ address, busyAddressId, confirmDeleteId, onCancelDelete, onDelete, onEdit, onRequestDelete, onSelect, onSetDefault, selected }: { address: CustomerAddress; busyAddressId: string | null; confirmDeleteId: string | null; onCancelDelete: () => void; onDelete: () => void; onEdit: () => void; onRequestDelete: () => void; onSelect: () => void; onSetDefault: () => void; selected: boolean }) {
  const busy = busyAddressId === address.id;
  return <div className={`border bg-white p-4 ${selected ? "border-[var(--accent)] ring-1 ring-[var(--accent)]" : "border-[var(--line)]"}`}><label className="flex cursor-pointer items-start gap-3"><input aria-describedby={`address-${address.id}-details`} checked={selected} className="mt-1 size-4 accent-[var(--accent)]" disabled={busyAddressId !== null} name="shipping-address" onChange={onSelect} type="radio" value={address.id} /><span className="min-w-0 flex-1"><span className="flex flex-wrap items-center gap-2"><span className="font-medium">{address.label}</span>{address.isDefault ? <span className="border border-[#b9d7c6] bg-[#f4fbf6] px-2 py-0.5 text-xs font-medium text-[var(--accent-strong)]">Default</span> : null}{selected ? <span className="inline-flex items-center gap-1 text-xs font-medium text-[var(--accent)]"><Check aria-hidden="true" size={14} />Selected</span> : null}</span><span className="mt-1 block text-sm text-[var(--muted)]" id={`address-${address.id}-details`}>{address.recipientName}<br />{address.line1}{address.line2 ? <><br />{address.line2}</> : null}<br />{address.city}, {address.countryCode}{address.postalCode ? ` ${address.postalCode}` : ""}</span></span></label><div className="mt-4 flex flex-wrap gap-2 border-t border-[var(--line)] pt-3"><button className="inline-flex h-8 items-center gap-1 px-2 text-sm font-medium text-[var(--accent)] hover:bg-[#e9f2ed] disabled:opacity-60" disabled={busyAddressId !== null} onClick={onEdit} type="button"><Pencil aria-hidden="true" size={14} />Edit</button>{!address.isDefault ? <button className="inline-flex h-8 items-center gap-1 px-2 text-sm font-medium text-[var(--accent)] hover:bg-[#e9f2ed] disabled:opacity-60" disabled={busyAddressId !== null} onClick={onSetDefault} type="button">{busy ? <LoaderCircle aria-hidden="true" className="animate-spin" size={14} /> : null}Set default</button> : null}<button className="inline-flex h-8 items-center gap-1 px-2 text-sm font-medium text-[var(--danger)] hover:bg-[#fff7f6] disabled:opacity-60" disabled={busyAddressId !== null} onClick={onRequestDelete} type="button"><Trash2 aria-hidden="true" size={14} />Delete</button></div>{confirmDeleteId === address.id ? <div className="mt-3 border border-[#f3c5c1] bg-[#fff7f6] p-3 text-sm"><p className="text-[var(--danger)]">Delete “{address.label}”? It will no longer be available for new orders.</p><div className="mt-3 flex gap-2"><button className="h-8 border border-[var(--line)] px-3 font-medium hover:bg-white" disabled={busy} onClick={onCancelDelete} type="button">Keep</button><button className="inline-flex h-8 items-center gap-1 bg-[var(--danger)] px-3 font-semibold text-white disabled:opacity-60" disabled={busy} onClick={onDelete} type="button">{busy ? <LoaderCircle aria-hidden="true" className="animate-spin" size={14} /> : null}Delete address</button></div></div> : null}</div>;
}

function AddressForm({ address, busy, onCancel, onSubmit }: { address: CustomerAddress | null; busy: boolean; onCancel: () => void; onSubmit: (input: AddressInput) => void }) {
  const initial = address ? { label: address.label, recipientName: address.recipientName, line1: address.line1, line2: address.line2, city: address.city, countryCode: address.countryCode, postalCode: address.postalCode, makeDefault: address.isDefault } : emptyAddress;
  const [input, setInput] = useState<AddressInput>(initial);
  function update(name: keyof AddressInput, value: string | boolean) { setInput((current) => ({ ...current, [name]: value })); }
  function submit(event: React.FormEvent<HTMLFormElement>) { event.preventDefault(); onSubmit({ ...input, label: input.label.trim(), recipientName: input.recipientName.trim(), line1: input.line1.trim(), line2: input.line2?.trim() || null, city: input.city.trim(), countryCode: input.countryCode.trim().toUpperCase(), postalCode: input.postalCode?.trim().toUpperCase() || null }); }
  return <form className="mt-5 border border-[var(--line)] bg-[#fbfcfa] p-4" onSubmit={submit}><div className="flex items-center justify-between gap-3"><h4 className="font-semibold">{address ? "Edit address" : "Add address"}</h4><button aria-label="Close address form" className="inline-flex h-8 items-center gap-1 text-sm text-[var(--muted)] hover:text-[var(--foreground)]" disabled={busy} onClick={onCancel} type="button"><ChevronDown aria-hidden="true" size={16} />Close</button></div><div className="mt-4 grid gap-3 sm:grid-cols-2"><Field label="Label" name="label" onChange={update} required value={input.label} /><Field label="Recipient name" name="recipientName" onChange={update} required value={input.recipientName} /><Field label="Address line 1" name="line1" onChange={update} required value={input.line1} /><Field label="Address line 2" name="line2" onChange={update} value={input.line2 ?? ""} /><Field label="City" name="city" onChange={update} required value={input.city} /><Field label="Country code" maxLength={2} name="countryCode" onChange={update} pattern="[A-Za-z]{2}" required value={input.countryCode} /><Field label="Postal code" name="postalCode" onChange={update} value={input.postalCode ?? ""} /></div><label className="mt-4 flex items-start gap-2 text-sm"><input checked={input.makeDefault} className="mt-0.5 size-4 accent-[var(--accent)]" onChange={(event) => update("makeDefault", event.target.checked)} type="checkbox" /><span>Make this my default delivery address</span></label><div className="mt-5 flex flex-wrap gap-2"><button className="inline-flex h-10 items-center gap-2 bg-[var(--accent)] px-4 text-sm font-semibold text-white hover:bg-[var(--accent-strong)] disabled:opacity-60" disabled={busy} type="submit">{busy ? <LoaderCircle aria-hidden="true" className="animate-spin" size={16} /> : null}{busy ? "Saving" : address ? "Save address" : "Add address"}</button><button className="h-10 border border-[var(--line)] px-4 text-sm font-semibold hover:bg-white disabled:opacity-60" disabled={busy} onClick={onCancel} type="button">Cancel</button></div></form>;
}

function Field({ label, name, onChange, required, value, ...attributes }: { label: string; name: keyof AddressInput; onChange: (name: keyof AddressInput, value: string) => void; required?: boolean; value: string } & Omit<React.InputHTMLAttributes<HTMLInputElement>, "onChange" | "value">) {
  return <label className="block text-sm font-medium">{label}<input className="mt-1 h-10 w-full border border-[var(--line)] bg-white px-3 font-normal outline-none focus:border-[var(--accent)]" disabled={attributes.disabled} name={name} onChange={(event) => onChange(name, event.target.value)} required={required} value={value} {...attributes} /></label>;
}
