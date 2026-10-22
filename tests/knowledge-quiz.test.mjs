import test from 'node:test';
import assert from 'node:assert/strict';
import fs from 'node:fs';
import { JSDOM } from 'jsdom';
import { QUIZ_ID, QUESTIONS, MAX_ANSWER, ANSWER_IDS, validateAnswers, submitOnce, readSubmission, isKnowledgeQuizNote } from '../knowledge-quiz-core.js';
import { createKnowledgeQuiz } from '../knowledge-quiz.js';

const HER = 'respondent', HIS = 'author';
const values = (prefix = 'Мой ответ') => QUESTIONS.map((_, i) => `${prefix} ${i + 1} 💚`);
const flush = async () => { for (let i = 0; i < 70; i++) await Promise.resolve(); };

// A generic optimistic transaction store: reads capture a revision, commits are
// atomic, conflicting attempts retry. Tests never contact the real Firebase.
function backend() {
  const state = { records: new Map(), revision: 0, calls: 0, writes: 0, failCommit: false, cached: false, listeners: new Set(), onRead: null };
  const doc = (_, ...parts) => parts.join('/');
  const collection = doc;
  const snap = path => ({ id: path.split('/').at(-1), exists: () => state.records.has(path), data: () => ({ ...state.records.get(path) }) });
  const documents = () => [...state.records.keys()].map(snap);
  const snapshot = () => ({ docs: documents(), metadata: { fromCache: state.cached } });
  const notify = () => { for (const listener of [...state.listeners]) listener.next(snapshot()); };
  const runTransaction = async (_, callback) => {
    state.calls++;
    for (let retry = 0; retry < 6; retry++) {
      const revision = state.revision, pending = new Map();
      const result = await callback({
        get: async path => { await Promise.resolve(); state.onRead?.(path); return snap(path); },
        set: (path, data) => pending.set(path, { ...data })
      });
      if (revision !== state.revision) continue;
      if (state.failCommit) throw Object.assign(new Error('offline'), { code: 'unavailable' });
      if (pending.size) {
        for (const [path, data] of pending) state.records.set(path, data);
        state.writes += pending.size; state.revision++; notify();
      }
      return result;
    }
    throw new Error('Transaction retry limit');
  };
  const onSnapshot = (_, options, next, error) => {
    const listener = { next, error }; state.listeners.add(listener); queueMicrotask(() => { if (state.listeners.has(listener)) next(snapshot()); });
    return () => state.listeners.delete(listener);
  };
  const stamp = { toDate: () => new Date('2026-10-23T10:00:00Z') };
  return { state, doc, collection, onSnapshot, runTransaction, serverTimestamp: () => stamp, documents, notify, path: id => doc(null, 'notebooks', 'zvezdochka-shared-notebook', 'notes', id) };
}
function service(db, answers = values(), uid = HER) {
  return { auth: { currentUser: uid ? { uid } : null }, db, doc: db.doc, runTransaction: db.runTransaction, serverTimestamp: db.serverTimestamp, respondentUid: HER, answers };
}
async function ui(db = backend(), uid = HER, initialStorage = {}) {
  const dom = new JSDOM('<!doctype html><button id="knowledgeQuizOpen" disabled>Тест</button>', { url: 'https://example.test/polya/' });
  globalThis.window = dom.window; globalThis.document = dom.window.document;
  for (const [key, value] of Object.entries(initialStorage)) dom.window.localStorage.setItem(key, value);
  const auth = { currentUser: uid ? { uid } : null }, authListeners = [];
  let loginCalls = 0;
  const controller = createKnowledgeQuiz({ auth, db, ...db, respondentUid: HER, authorUid: HIS, onAuthStateChanged: (_, listener) => { authListeners.push(listener); listener(auth.currentUser); return () => {}; }, requestLogin: () => loginCalls++, finishLogin: () => {} });
  await flush(); controller.open();
  const click = text => { const target = [...document.querySelectorAll('button')].find(button => button.textContent === text); assert.ok(target, `Button exists: ${text}`); target.click(); };
  const input = value => { const target = document.querySelector('textarea'); assert.ok(target); target.value = value; target.dispatchEvent(new dom.window.Event('input', { bubbles: true })); };
  const authenticate = nextUid => { auth.currentUser = nextUid ? { uid: nextUid } : null; for (const listener of authListeners) listener(auth.currentUser); };
  return { db, auth, dom, controller, click, input, authenticate, get loginCalls() { return loginCalls; } };
}
async function fill(widget, entries = values()) {
  widget.click('Начнём? →');
  entries.forEach((value, i) => { widget.input(value); widget.click(i === 14 ? 'Проверить мои ответы →' : 'Дальше →'); });
}

test('15 unique questions, exact final wording and notebook-compatible lengths', () => {
  assert.equal(QUESTIONS.length, 15); assert.equal(new Set(ANSWER_IDS).size, 15);
  assert.equal(QUESTIONS[14].text, 'Ты будешь со мной до конца жизни? Будешь со мной до последнего? В проблемах и блаженстве?');
  assert.ok(QUESTIONS.every(q => q.text.length + 2 + MAX_ANSWER <= 590));
  assert.equal(isKnowledgeQuizNote(QUIZ_ID), true); assert.equal(isKnowledgeQuizNote(ANSWER_IDS[0]), true);
  assert.equal(isKnowledgeQuizNote(QUIZ_ID + '-ordinary-note'), false);
});
test('Saving creates one receipt and all fifteen answers atomically with the existing four-field schema', async () => {
  const db = backend(); const result = await submitOnce(service(db));
  assert.equal(result.created, true); assert.equal(db.state.records.size, 16);
  for (const data of db.state.records.values()) { assert.deepEqual(Object.keys(data).sort(), ['createdAt', 'done', 'text', 'updatedAt']); assert.ok(data.text.length <= 590); }
  assert.deepEqual(readSubmission(db.documents(), HER).answers, values());
});
test('Author, outsider and signed-out users cannot consume her attempt', async () => {
  const db = backend(); for (const uid of [HIS, 'outsider', null]) await assert.rejects(submitOnce(service(db, values(), uid)), { code: 'not-respondent' });
  assert.equal(db.state.calls, 0); assert.equal(db.state.records.size, 0);
});
test('Missing, blank, non-string and oversized answers never start a transaction', async () => {
  const db = backend(); const blank = values(); blank[7] = ' \n ';
  const invalid = values(); invalid[3] = null; const long = values(); long[0] = 'x'.repeat(401);
  for (const entries of [values().slice(1), blank, invalid, long]) await assert.rejects(submitOnce(service(db, entries)));
  assert.equal(db.state.calls, 0); assert.deepEqual(validateAnswers(values().map(value => `  ${value}\n`)), values());
});
test('A later submission cannot replace the first answers', async () => {
  const db = backend(); await submitOnce(service(db, values('Первый')));
  assert.equal((await submitOnce(service(db, values('Замена')))).created, false);
  assert.equal(db.state.writes, 16); assert.deepEqual(readSubmission(db.documents(), HER).answers, values('Первый'));
});
test('Concurrent devices produce exactly one winning submission', async () => {
  const db = backend(); const results = await Promise.all([submitOnce(service(db, values('А'))), submitOnce(service(db, values('Б')))]);
  assert.equal(results.filter(result => result.created).length, 1); assert.equal(db.state.writes, 16);
  const saved = readSubmission(db.documents(), HER).answers; assert.ok(saved.every(value => value.startsWith(saved[0][0])));
});
test('Failed commit leaves no partial answers or receipt', async () => {
  const db = backend(); db.state.failCommit = true; await assert.rejects(submitOnce(service(db)), { code: 'unavailable' });
  assert.equal(db.state.records.size, 0); assert.equal(db.state.writes, 0);
});
test('A session change during transaction reads prevents every write', async () => {
  const db = backend(), config = service(db); let reads = 0;
  db.state.onRead = () => { if (++reads === 2) config.auth.currentUser = { uid: HIS }; };
  await assert.rejects(submitOnce(config), { code: 'auth-changed' }); assert.equal(db.state.writes, 0);
});
test('A damaged receipt or existing partial submission is never overwritten', async () => {
  const db = backend(); db.state.records.set(db.path(ANSWER_IDS[4]), { text: 'existing' });
  await assert.rejects(submitOnce(service(db)), { code: 'partial-submission' }); assert.equal(db.state.records.size, 1);
  db.state.records.set(db.path(QUIZ_ID), { text: 'broken' }); assert.equal(readSubmission(db.documents(), HER).state, 'partial');
  assert.equal((await submitOnce(service(db))).created, false); assert.equal(db.state.writes, 0);
});
test('Signed-out UI opens existing login and restores focus when closing', async () => {
  const widget = await ui(backend(), null); widget.click('Войти через блокнот →');
  assert.equal(widget.loginCalls, 1); assert.equal(document.querySelector('dialog').open, false); assert.equal(document.body.style.overflow, '');
});
test('His account only waits for her answers and has no submission form', async () => {
  await ui(backend(), HIS); assert.match(document.body.textContent, /Жду её ответы/); assert.equal(document.querySelector('textarea'), null); assert.equal(document.querySelector('#knowledgeQuizSubmit'), null);
});
test('Fifteen real inputs remain drafts until final submission; review can edit them', async () => {
  const widget = await ui(); await fill(widget); assert.equal(widget.db.state.records.size, 0); assert.ok(window.localStorage.length > 0);
  document.querySelector('.kq-edit').click(); widget.input('Другой любимый цвет'); widget.click('К проверке ответов →');
  assert.match(document.querySelector('.kq-review-list').textContent, /Другой любимый цвет/);
  widget.click('Сохранить мои ответы 💌'); await flush();
  assert.equal(widget.db.state.records.size, 16); assert.match(document.body.textContent, /Ответы сохранены/); assert.equal(document.querySelector('textarea'), null); assert.equal(window.localStorage.length, 0);
  assert.equal(readSubmission(widget.db.documents(), HER).answers[0], 'Другой любимый цвет');
});
test('Double-clicking the final action starts only one transaction', async () => {
  const widget = await ui(); await fill(widget); const target = document.querySelector('#knowledgeQuizSubmit'); target.click(); target.click(); await flush();
  assert.equal(widget.db.state.calls, 1); assert.equal(widget.db.state.writes, 16);
});
test('Reload and another account show all saved answers read-only', async () => {
  const db = backend(); await submitOnce(service(db));
  for (const uid of [HER, HIS]) { await ui(db, uid); assert.equal(document.querySelectorAll('.kq-review-item').length, 15); assert.equal(document.querySelector('textarea'), null); assert.equal(document.querySelector('.kq-edit'), null); assert.equal(document.querySelector('#knowledgeQuizSubmit'), null); }
  assert.equal(db.state.writes, 16);
});
test('Draft survives a reload but is isolated from his account', async () => {
  const first = await ui(); first.click('Начнём? →'); first.input('Мой сохранённый черновик');
  const draft = Object.fromEntries(Object.keys(window.localStorage).map(key => [key, window.localStorage.getItem(key)]));
  await ui(backend(), HIS, draft); assert.doesNotMatch(document.body.textContent, /Мой сохранённый черновик/);
  const restored = await ui(backend(), HER, draft); restored.click('Продолжить мой черновик →'); assert.equal(document.querySelector('textarea').value, 'Мой сохранённый черновик');
});
test('A failed save retains the draft, allows retry and never shows saved success', async () => {
  const widget = await ui(); await fill(widget); widget.db.state.failCommit = true; widget.click('Сохранить мои ответы 💌'); await flush();
  assert.equal(widget.db.state.records.size, 0); assert.match(document.body.textContent, /Не удалось сохранить/); assert.doesNotMatch(document.body.textContent, /Ответы сохранены/); assert.ok(window.localStorage.length > 0);
  widget.db.state.failCommit = false; widget.click('Сохранить мои ответы 💌'); await flush(); assert.equal(widget.db.state.records.size, 16);
});
test('A cached absence cannot start the test before server confirmation', async () => {
  const db = backend(); db.state.cached = true; await ui(db); assert.equal(document.querySelector('textarea'), null); assert.match(document.body.textContent, /проверяем, не был ли тест уже пройден/);
  db.state.cached = false; db.notify(); assert.match(document.body.textContent, /Начнём/);
});
test('An empty answer shows a local error without advancing or writing', async () => {
  const widget = await ui(); widget.click('Начнём? →'); widget.click('Дальше →');
  assert.equal(document.querySelector('textarea').name, 'color'); assert.equal(document.querySelector('textarea').getAttribute('aria-invalid'), 'true'); assert.equal(widget.db.state.writes, 0);
});
test('Answers containing HTML render only as text', async () => {
  const db = backend(), entries = values(); entries[0] = '<img src=x onerror="alert(1)"><script>alert(1)</script>';
  await submitOnce(service(db, entries)); await ui(db, HIS);
  assert.equal(document.querySelector('.kq-review-list img'), null); assert.equal(document.querySelector('.kq-review-list script'), null); assert.ok(document.querySelector('.kq-review-list').textContent.includes(entries[0]));
});
test('A completion on another device immediately locks an open draft', async () => {
  const widget = await ui(); widget.click('Начнём? →'); widget.input('Черновик'); await submitOnce(service(widget.db, values('Другое устройство'))); await flush();
  assert.equal(document.querySelector('textarea'), null); assert.match(document.body.textContent, /Другое устройство/); assert.equal(window.localStorage.length, 0);
});
test('A stale listener cannot reveal saved answers after logout', async () => {
  const db = backend(); await submitOnce(service(db)); const widget = await ui(db, HIS); const old = [...db.state.listeners][0]; widget.authenticate(null);
  old.next({ docs: db.documents(), metadata: { fromCache: false } }); assert.equal(document.querySelector('.kq-review-list'), null); assert.match(document.body.textContent, /Войти через блокнот/);
});
test('Listener failure revokes submission readiness and retry reconnects', async () => {
  const widget = await ui(); const listener = [...widget.db.state.listeners][0]; listener.error(new Error('unavailable'));
  assert.match(document.body.textContent, /Пока не удалось/); assert.equal(document.querySelector('#knowledgeQuizSubmit'), null);
  widget.click('Попробовать ещё раз'); await flush(); assert.match(document.body.textContent, /Начнём/);
});
test('A cache transition keeps the active question and typed answer', async () => {
  const widget = await ui(); widget.click('Начнём? →'); widget.input('Мой цвет');
  widget.db.state.cached = true; widget.db.notify(); widget.db.state.cached = false; widget.db.notify();
  assert.equal(document.querySelector('textarea').value, 'Мой цвет'); assert.equal(document.querySelector('textarea').name, 'color');
});
test('A full local-storage quota still permits completing and saving the test', async () => {
  const widget = await ui(); Object.defineProperty(widget.dom.window.Storage.prototype, 'setItem', { value: () => { throw new Error('quota'); } });
  await fill(widget); widget.click('Сохранить мои ответы 💌'); await flush(); assert.equal(widget.db.state.records.size, 16); assert.match(document.body.textContent, /Ответы сохранены/);
});
test('An incomplete server receipt locks the form instead of offering another attempt', async () => {
  const db = backend(); db.state.records.set(db.path(QUIZ_ID), { text: 'damaged' }); await ui(db);
  assert.match(document.body.textContent, /Письмо уже существует/); assert.equal(document.querySelector('textarea'), null); assert.equal(document.querySelector('#knowledgeQuizSubmit'), null);
});
test('The actual notebook module keeps tasks intact and exposes one read-only quiz card', async () => {
  const html = fs.readFileSync(new URL('../index.html', import.meta.url), 'utf8');
  const moduleText = [...html.matchAll(/<script type="module">([\s\S]*?)<\/script>/g)].find(match => match[1].includes('ОБЩИЙ БЛОКНОТ'))[1];
  const source = moduleText.replace(/^import\s+[\s\S]*?from\s+["'][^"']+["'];?/gm, '');
  const db = backend(), respondentUid = 'BYmhRqqXQQPKPTOnrjzEFTfNL3D3';
  await submitOnce({ ...service(db), respondentUid, auth: { currentUser: { uid: respondentUid } } });
  db.state.records.set(db.path('ordinary-note'), { text: 'Наша прежняя задача', done: false });
  const dom = new JSDOM(html, { url: 'https://example.test/polya/' });
  globalThis.window = dom.window; globalThis.document = dom.window.document; globalThis.localStorage = dom.window.localStorage;
  const auth = { currentUser: { uid: 'IG3jAerjnHZekX8gEn51AQq6Oli1', email: 'author@example.test' } };
  const dependencies = {
    createKnowledgeQuiz, QUIZ_ID, isKnowledgeQuizNote, ...db,
    initializeApp: () => ({}), getAuth: () => auth, setPersistence: async () => {}, browserLocalPersistence: {},
    signInWithEmailAndPassword: async () => { throw new Error('No real sign-in in tests'); }, signOut: async () => {},
    onAuthStateChanged: (_, listener) => { queueMicrotask(() => listener(auth.currentUser)); return () => {}; },
    initializeFirestore: () => db, getFirestore: () => db, persistentLocalCache: () => ({}), persistentMultipleTabManager: () => ({}),
    getDocs: async () => ({ empty: false, docs: db.documents() }),
    addDoc: () => { throw new Error('Unexpected create'); }, updateDoc: () => { throw new Error('Unexpected update'); }, deleteDoc: () => { throw new Error('Unexpected delete'); }, writeBatch: () => { throw new Error('Unexpected batch'); }
  };
  new Function(...Object.keys(dependencies), source)(...Object.values(dependencies));
  await flush();
  const list = document.querySelector('#starNoteList'); assert.equal(list.querySelectorAll('.star-note-item').length, 2);
  assert.match(list.textContent, /Наша прежняя задача/); assert.equal(list.querySelectorAll('.star-note-check').length, 1);
  const card = list.querySelector('.kq-note-card'); assert.ok(card); assert.equal(card.querySelector('.star-note-action'), null);
  assert.match(document.querySelector('#starNoteCounter').textContent, /^1 задача/);
  card.querySelector('button').click(); assert.equal(document.querySelector('dialog').open, true); assert.equal(document.querySelectorAll('.kq-review-item').length, 15);
  assert.equal(db.state.writes, 16);
});
