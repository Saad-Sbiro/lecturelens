import terms from '../../config/terms.json';
import text from '../../config/browser-ui.json';

export let termsAccepted = false;
const el = <T extends HTMLElement>(id: string) => document.getElementById(id) as T;

export async function initializeAgreement(changed: () => void) {
  const dialog = el<HTMLDialogElement>('termsDialog');
  el('termsTitle').textContent = terms.title;
  el('closeTerms').textContent = terms.close;
  const intro = document.createElement('p'); intro.textContent = terms.intro;
  el('termsContent').append(intro);
  for (const section of terms.sections) {
    const heading = document.createElement('h3'); heading.textContent = section.title;
    const body = document.createElement('p'); body.textContent = section.body;
    el('termsContent').append(heading, body);
  }
  for (const id of ['readTerms', 'termsLink']) el(id).onclick = () => dialog.showModal();
  el('closeTerms').onclick = () => dialog.close();
  const check = el<HTMLInputElement>('consent'), accept = el<HTMLButtonElement>('acceptTerms');
  check.onchange = () => { accept.disabled = !check.checked; };
  function show(accepted: boolean) {
    termsAccepted = accepted;
    el('agreementNotice').hidden = accepted;
    changed();
  }
  accept.onclick = async () => {
    if (!check.checked || accept.disabled) return;
    accept.disabled = true; check.disabled = true;
    try {
      const reply = await chrome.runtime.sendMessage({ target: 'background', op: 'acceptTerms', accepted: true });
      if (!reply?.ok || reply.value?.accepted !== true) throw new Error(reply?.error ?? terms.saveError);
      el('error').textContent = ''; show(true);
    } catch (error) { el('error').textContent = error instanceof Error ? error.message : terms.saveError; }
    finally { check.disabled = false; accept.disabled = !check.checked; }
  };
  try {
    const reply = await chrome.runtime.sendMessage({ target: 'background', op: 'termsStatus' });
    if (!reply?.ok) throw new Error(reply?.error ?? text.connectFailed);
    show(reply.value?.accepted === true);
  } catch (error) {
    el('error').textContent = error instanceof Error ? error.message : text.connectFailed;
    show(false);
  }
}
