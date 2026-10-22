import { QUIZ_ID, QUESTIONS, MAX_ANSWER, submitOnce, readSubmission } from './knowledge-quiz-core.js';

export function createKnowledgeQuiz({ auth, db, collection, doc, onSnapshot, onAuthStateChanged, runTransaction, serverTimestamp, respondentUid, authorUid, requestLogin, finishLogin }) {
  const opener = document.getElementById('knowledgeQuizOpen');
  const dialog = document.createElement('dialog');
  dialog.className = 'knowquiz-dialog';
  dialog.id = 'knowledgeQuizDialog';
  dialog.setAttribute('aria-labelledby', 'knowledgeQuizTitle');
  dialog.innerHTML = `<div class="kq-shell"><div class="kq-top"><span class="kq-eyebrow">Для нас двоих · маленькое письмо</span><button class="kq-close" type="button" aria-label="Закрыть тест">×</button></div><div class="kq-body"></div></div>`;
  document.body.append(dialog);
  const body = dialog.querySelector('.kq-body');
  let user = null, generation = 0, unsubscribe = null, phase = 'loading', index = 0;
  let answers = QUESTIONS.map(() => ''), submission = null, serverReady = false, inFlight = false, editingReview = false;
  let message = '', storage = null, draftPersisted = true, resumeAfterLogin = false, previousFocus = null, previousOverflow = '';
  try { storage = window.localStorage; } catch { /* The form still works without local storage. */ }
  const isHer = () => user?.uid === respondentUid;
  const allowed = value => value && [respondentUid, authorUid].includes(value.uid);
  const draftKey = () => `${QUIZ_ID}:draft:${respondentUid}`;
  const node = (tag, className, text) => {
    const result = document.createElement(tag);
    if (className) result.className = className;
    if (text !== undefined) result.textContent = text;
    return result;
  };
  const button = (text, click, primary = false) => {
    const result = node('button', `kq-button${primary ? ' kq-primary' : ''}`, text);
    result.type = 'button'; result.addEventListener('click', click); return result;
  };
  function saveDraft() {
    try {
      if (!storage) throw new Error('Storage unavailable');
      storage.setItem(draftKey(), JSON.stringify({ version: 1, answers, index })); draftPersisted = true;
    } catch { draftPersisted = false; }
  }
  function clearDraft() { try { storage?.removeItem(draftKey()); } catch { /* Cloud submission remains authoritative. */ } }
  function restoreDraft() {
    answers = QUESTIONS.map(() => ''); index = 0; draftPersisted = true;
    try {
      const draft = JSON.parse(storage?.getItem(draftKey()) || 'null');
      if (draft?.version !== 1 || !Array.isArray(draft.answers) || draft.answers.length !== QUESTIONS.length) return;
      if (!draft.answers.every(value => typeof value === 'string' && value.length <= MAX_ANSWER)) return;
      answers = draft.answers.slice(); index = Math.max(0, Math.min(14, Number.isInteger(draft.index) ? draft.index : 0));
    } catch { /* A damaged draft must not affect the server receipt. */ }
  }
  function open() {
    if (!dialog.open) {
      previousFocus = document.activeElement; previousOverflow = document.body.style.overflow;
      if (typeof dialog.showModal === 'function') dialog.showModal(); else dialog.setAttribute('open', '');
      document.body.style.overflow = 'hidden';
    }
    render();
  }
  function close() {
    if (!dialog.open) return;
    if (typeof dialog.close === 'function') dialog.close(); else dialog.removeAttribute('open');
    document.body.style.overflow = previousOverflow;
    if (previousFocus?.isConnected) previousFocus.focus();
  }
  dialog.querySelector('.kq-close').addEventListener('click', close);
  dialog.addEventListener('cancel', event => { event.preventDefault(); close(); });
  dialog.addEventListener('keydown', event => {
    if (event.key !== 'Tab') return;
    const controls = [...dialog.querySelectorAll('button:not(:disabled),textarea:not(:disabled),a[href]')];
    const first = controls[0], last = controls[controls.length - 1];
    if (event.shiftKey && document.activeElement === first) { event.preventDefault(); last?.focus(); }
    else if (!event.shiftKey && document.activeElement === last) { event.preventDefault(); first?.focus(); }
  });
  opener?.addEventListener('click', open);
  if (opener) { opener.disabled = false; opener.removeAttribute('aria-busy'); }
  function title(text) {
    const heading = node('h2', '', text); heading.id = 'knowledgeQuizTitle'; heading.tabIndex = -1; body.append(heading); return heading;
  }
  function status(text = message) {
    const result = node('p', 'kq-status', text); result.setAttribute('role', 'status'); result.setAttribute('aria-live', 'polite'); body.append(result); return result;
  }
  function footer(text) { body.append(node('div', 'kq-dialog-footer', text)); }
  function focusHeading() {
    if (!dialog.open) return;
    dialog.scrollTop = 0;
    body.querySelector('h2')?.focus({ preventScroll: true });
  }
  function envelope() { const item = node('div', 'kq-envelope'); item.setAttribute('aria-hidden', 'true'); item.append(node('span', '', '💌')); body.append(item); }
  function login() { resumeAfterLogin = true; close(); requestLogin(); }
  function renderIntro() {
    envelope(); title('Насколько ты меня знаешь?');
    body.append(node('p', 'kq-lead', 'Про мои маленькие привычки, наши обнимашки и немного про инопланетян. Оставь здесь свои ответы — я сохраню и прочитаю каждый.'));
    const tags = node('div', 'kq-tags'); ['15 вопросов', 'Твои слова', 'Одна отправка'].forEach(text => tags.append(node('span', '', text))); body.append(tags);
    body.append(node('div', 'kq-callout', 'До отправки можно возвращаться к вопросам и менять черновик. После сохранения ответы останутся такими, какими ты их написала. Пройти тест повторно не получится.'));
    body.append(button(answers.some(value => value.trim()) ? 'Продолжить мой черновик →' : 'Начнём? →', () => { phase = 'question'; message = ''; render(); }, true));
    footer('Ответы будут доступны в этом тесте и в вашем общем блокноте.');
  }
  function renderQuestion() {
    const question = QUESTIONS[index];
    const label = node('div', 'kq-progress-label'); label.append(node('span', '', `Вопрос ${String(index + 1).padStart(2, '0')} / 15`), node('span', '', `${answers.filter(value => value.trim()).length} ответов в черновике`)); body.append(label);
    const progress = node('div', 'kq-progress'); progress.setAttribute('role', 'progressbar'); progress.setAttribute('aria-label', 'Прогресс теста'); progress.setAttribute('aria-valuemin', '0'); progress.setAttribute('aria-valuemax', '15'); progress.setAttribute('aria-valuenow', String(index + 1));
    QUESTIONS.forEach((_, i) => { const part = node('i'); if (answers[i].trim()) part.dataset.filled = ''; if (i === index) part.dataset.current = ''; progress.append(part); }); body.append(progress);
    body.append(node('div', 'kq-number', index === 14 ? 'Самый важный вопрос' : index >= 10 ? 'Немного несерьёзности' : 'Маленькая деталь обо мне'));
    title(question.text).className = `kq-question${index === 14 ? ' kq-last' : ''}`;
    body.append(node('p', 'kq-hint', question.hint));
    const answerLabel = node('label', 'kq-answer-label', 'Твой ответ'); answerLabel.htmlFor = 'knowledgeQuizAnswer';
    const input = node('textarea', 'kq-answer'); input.id = 'knowledgeQuizAnswer'; input.name = question.id; input.maxLength = MAX_ANSWER; input.rows = 4; input.value = answers[index]; input.placeholder = index === 14 ? 'Напиши так, как чувствуешь…' : 'Мне кажется, что…'; input.setAttribute('aria-describedby', 'knowledgeQuizCount knowledgeQuizFeedback');
    body.append(answerLabel, input);
    const meta = node('div', 'kq-input-meta'), draftLabel = node('span', '', draftPersisted ? 'Черновик на этом устройстве' : 'Черновик в этой вкладке'), count = node('span', '', `${input.value.length} / ${MAX_ANSWER}`); count.id = 'knowledgeQuizCount'; meta.append(draftLabel, count); body.append(meta);
    const feedback = status(); feedback.id = 'knowledgeQuizFeedback';
    input.addEventListener('input', () => { answers[index] = input.value; saveDraft(); draftLabel.textContent = draftPersisted ? 'Черновик на этом устройстве' : 'Черновик в этой вкладке'; count.textContent = `${input.value.length} / ${MAX_ANSWER}`; feedback.textContent = ''; input.removeAttribute('aria-invalid'); });
    const actions = node('div', 'kq-actions');
    actions.append(button(index === 0 ? 'К началу' : '← Назад', () => { phase = index === 0 ? 'intro' : 'question'; if (index > 0) index--; editingReview = false; saveDraft(); message = ''; render(); }));
    actions.append(button(editingReview ? 'К проверке ответов →' : index === 14 ? 'Проверить мои ответы →' : 'Дальше →', () => {
      if (!input.value.trim()) { feedback.textContent = 'Напиши свой ответ, чтобы продолжить.'; input.setAttribute('aria-invalid', 'true'); input.focus(); return; }
      answers[index] = input.value; message = '';
      if (editingReview || index === 14) { phase = 'review'; editingReview = false; } else index++;
      saveDraft(); render();
    }, true)); body.append(actions);
    footer('Пока это черновик. Я увижу ответы после твоего последнего нажатия «Сохранить мои ответы».');
  }
  function answerList(values, editable) {
    const list = node('ol', 'kq-review-list');
    QUESTIONS.forEach((question, i) => {
      const card = node('li', 'kq-review-item'); card.append(node('span', 'kq-number', `Вопрос ${String(i + 1).padStart(2, '0')}`), node('h3', '', question.text), node('p', '', values[i]));
      if (editable) {
        const edit = button('Изменить ответ', () => { index = i; phase = 'question'; editingReview = true; message = ''; render(); }); edit.className = 'kq-edit'; edit.disabled = inFlight; card.append(edit);
      }
      list.append(card);
    }); return list;
  }
  function renderReview() {
    title('Проверим перед отправкой?');
    body.append(node('p', 'kq-lead', 'Все 15 ответов собраны. Перечитай их, если хочется, и сохрани это маленькое письмо для меня.'));
    body.append(answerList(answers, true));
    body.append(node('div', 'kq-callout', 'Отправить можно только один раз. После сохранения изменить или заменить ответы нельзя.'));
    const feedback = status(); feedback.id = 'knowledgeQuizSaveStatus';
    const actions = node('div', 'kq-actions'); const back = button('← К последнему вопросу', () => { index = 14; phase = 'question'; render(); }); back.disabled = inFlight; actions.append(back);
    const save = button(inFlight ? 'Сохраняем твоё письмо…' : 'Сохранить мои ответы 💌', submit, true); save.id = 'knowledgeQuizSubmit'; save.disabled = inFlight || !serverReady; actions.append(save); body.append(actions);
    if (!serverReady && !inFlight) { feedback.textContent = 'Для отправки нужно подключение к интернету. Черновик остаётся у тебя.'; actions.append(button('Повторить подключение', connect)); }
    footer('Сохранённые ответы будут доступны здесь и в общем блокноте.');
  }
  function renderSaved() {
    body.append(node('div', 'kq-saved-mark', '✓')); title('Ответы сохранены 💌');
    body.append(node('p', 'kq-lead', isHer() ? 'Спасибо за твои слова. Я смогу открыть это письмо со своего аккаунта и прочитать все ответы.' : 'Все её ответы — здесь. Можно возвращаться к этому письму в любое время.'));
    let date = null; try { date = submission.submittedAt?.toDate?.(); } catch { /* A pending server timestamp has no date yet. */ }
    const tags = node('div', 'kq-tags'); tags.append(node('span', '', '15 ответов · только для чтения'));
    if (date && Number.isFinite(date.getTime())) tags.append(node('span', '', new Intl.DateTimeFormat('ru-RU', { dateStyle: 'long', timeStyle: 'short' }).format(date))); body.append(tags);
    body.append(answerList(submission.answers, false));
    body.append(button('Скачать ответы .txt', download));
    footer('Тест уже пройден. Повторная отправка недоступна, в том числе с другого устройства.');
  }
  function download() {
    if (submission?.state !== 'saved') return;
    const text = ['Насколько ты меня знаешь?', '', ...QUESTIONS.flatMap((q, i) => [`${i + 1}. ${q.text}`, submission.answers[i], ''])].join('\n');
    const url = URL.createObjectURL(new Blob([text], { type: 'text/plain;charset=utf-8' }));
    const link = document.createElement('a'); link.href = url; link.download = 'насколько-ты-меня-знаешь.txt'; link.click(); setTimeout(() => URL.revokeObjectURL(url), 1000);
  }
  function render() {
    body.replaceChildren();
    if (!allowed(user)) {
      envelope(); title('Насколько ты меня знаешь?'); body.append(node('p', 'kq-lead', 'Войди в свой аккаунт, чтобы открыть тест или посмотреть сохранённые ответы. Используется тот же вход, что и в вашем общем блокноте.')); body.append(button('Войти через блокнот →', login, true));
    } else if (phase === 'error') {
      title('Пока не удалось открыть письмо'); body.append(node('p', 'kq-lead', message)); body.append(button('Попробовать ещё раз', connect, true));
    } else if (submission?.state === 'saved') renderSaved();
    else if (submission?.state === 'partial' && serverReady) {
      title('Письмо уже существует'); body.append(node('p', 'kq-lead', 'Часть сохранённых ответов сейчас недоступна. Повторная отправка закрыта, чтобы сохранить прежнее письмо. Попробуй загрузить его ещё раз.')); body.append(button('Загрузить ответы', connect, true));
    } else if (!serverReady || phase === 'loading') {
      title('Открываем наше письмо…'); body.append(node('p', 'kq-lead', 'Подключаемся и проверяем, не был ли тест уже пройден.')); body.append(button('Повторить подключение', connect));
    } else if (!isHer()) {
      envelope(); title('Жду её ответы'); body.append(node('p', 'kq-lead', 'Когда она пройдёт тест, здесь появятся все 15 ответов. Они также будут доступны из общего блокнота. Эта страница обновится сама.')); footer('Твой аккаунт открывает сохранённые ответы. Сам тест предназначен для неё.');
    } else if (phase === 'question') renderQuestion();
    else if (phase === 'review') renderReview();
    else renderIntro();
    focusHeading();
  }
  async function submit() {
    if (inFlight || !serverReady || !isHer() || submission?.state === 'saved') return;
    const empty = answers.findIndex(value => !value.trim());
    if (empty !== -1) { index = empty; phase = 'question'; message = 'Остался один маленький ответ.'; render(); return; }
    const currentGeneration = generation; inFlight = true; message = 'Сохраняем все 15 ответов…'; render();
    try {
      const result = await submitOnce({ auth, db, doc, runTransaction, serverTimestamp, respondentUid, answers: answers.slice() });
      if (generation !== currentGeneration) return;
      if (result.created && submission?.state !== 'saved') submission = { state: 'saved', answers: result.answers, submittedAt: null };
      if (result.created || submission?.state === 'saved') { clearDraft(); phase = 'saved'; }
      else { phase = 'loading'; message = ''; }
    } catch (error) {
      if (generation !== currentGeneration) return;
      phase = 'review';
      message = error?.code === 'partial-submission' ? 'Письмо уже начало сохраняться на другом устройстве. Нажми «Повторить подключение», чтобы загрузить его.' : 'Не удалось сохранить ответы. Черновик остался у тебя — проверь подключение и попробуй снова.';
      if (error?.code === 'partial-submission') { phase = 'error'; serverReady = false; }
    } finally {
      if (generation === currentGeneration) { inFlight = false; render(); }
    }
  }
  function connect() {
    unsubscribe?.(); unsubscribe = null; serverReady = false; message = ''; phase = 'loading'; render();
    if (!allowed(user)) return;
    const currentGeneration = generation;
    const ref = collection(db, 'notebooks', 'zvezdochka-shared-notebook', 'notes');
    unsubscribe = onSnapshot(ref, { includeMetadataChanges: true }, snapshot => {
      if (generation !== currentGeneration) return;
      const next = readSubmission(snapshot.docs, respondentUid); serverReady = !snapshot.metadata.fromCache;
      if (next.state === 'saved') { submission = next; phase = 'saved'; clearDraft(); render(); return; }
      if (submission?.state === 'saved' && next.state !== 'saved') return;
      submission = next;
      if (['loading', 'error', 'saved'].includes(phase)) phase = 'intro';
      if (phase === 'question' && next.state === 'empty') return;
      render();
    }, () => {
      if (generation !== currentGeneration) return;
      serverReady = false; phase = 'error'; message = 'Не получилось загрузить сохранённые ответы. Проверь интернет или войди в свой аккаунт ещё раз. Твой черновик остаётся на этом устройстве.'; render();
    });
  }
  onAuthStateChanged(auth, value => {
    generation++; unsubscribe?.(); unsubscribe = null; user = allowed(value) ? value : null;
    inFlight = false; serverReady = false; submission = null; phase = 'loading'; message = ''; editingReview = false;
    answers = QUESTIONS.map(() => ''); index = 0;
    if (isHer()) restoreDraft();
    if (user) connect(); else render();
    if (user && resumeAfterLogin) { resumeAfterLogin = false; finishLogin(); open(); }
  });
  return { open, close };
}
