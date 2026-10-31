/**
 * Hands the browser a text file to save (the console's CSV exports): a Blob under a short-lived object URL and a
 * click on an anchor the page never shows. Nothing leaves the page but to the person's disk.
 */
export const saveTextFile = (
  document: Document,
  fileName: string,
  text: string,
  type: string,
): void => {
  const view = document.defaultView;
  if (view === null) return;
  const url = view.URL.createObjectURL(new Blob([text], { type }));
  const link = document.createElement('a');
  link.href = url;
  link.download = fileName;
  link.click();
  view.URL.revokeObjectURL(url);
};
