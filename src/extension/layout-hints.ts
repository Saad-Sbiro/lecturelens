// Runs only in the explicitly selected tab under activeTab. Returns geometry,
// never page text, chat, URLs, credentials or DOM contents.
export function presentationRects() {
  return Array.from(document.querySelectorAll('video, canvas, svg')).map(element => {
    const box = element.getBoundingClientRect(); const style = getComputedStyle(element);
    const left = Math.max(0, box.left), top = Math.max(0, box.top);
    const right = Math.min(innerWidth, box.right), bottom = Math.min(innerHeight, box.bottom);
    return { x: left / innerWidth, y: top / innerHeight, width: (right - left) / innerWidth, height: (bottom - top) / innerHeight, visible: style.visibility !== 'hidden' && style.display !== 'none' && Number(style.opacity) > 0 };
  }).filter(r => r.visible && r.width * r.height > 0.15 && r.width > 0 && r.height > 0)
    .sort((a, b) => b.width * b.height - a.width * a.height).slice(0, 4)
    .map(({ x, y, width, height }) => ({ x, y, width, height }));
}
