export const QUIZ_ID = 'knowledge-quiz-v1';
export const MAX_ANSWER = 400;
export const QUESTIONS = Object.freeze([
  { id: 'color', text: 'Мой любимый цвет?', hint: 'Начнём с маленькой, но важной детали 🎨' },
  { id: 'food', text: 'Какое блюдо я готов есть хоть каждый день?', hint: 'Даже если холодильник уже устал от этого блюда.' },
  { id: 'drink', text: 'Что я обычно выбираю: чай, кофе или что-нибудь другое?', hint: 'Представь, что заказываешь напиток для меня ☕' },
  { id: 'birthday', text: 'Когда у меня день рождения?', hint: 'Тот самый день, когда миру выдали меня 🎂' },
  { id: 'movie', text: 'Какой мой любимый фильм или сериал?', hint: 'Можно вспомнить тот, который я готов пересматривать.' },
  { id: 'song', text: 'Какая песня у меня ассоциируется с нами?', hint: 'Та самая, от которой сразу хочется быть ближе 🎶' },
  { id: 'weekend', text: 'Как выглядит мой идеальный выходной?', hint: 'Можно составить целый маленький план.' },
  { id: 'annoyed', text: 'Что меня быстрее всего выводит из себя?', hint: 'Да-да, у этого милого человека тоже есть кнопка «ворчу».' },
  { id: 'comfort', text: 'Что лучше всего помогает мне, когда я устал или расстроен?', hint: 'Твой личный способ вернуть мне улыбку 💚' },
  { id: 'dream', text: 'Какую мою мечту ты помнишь?', hint: 'Большую, маленькую или пока совсем секретную ✨' },
  { id: 'cat', text: 'Если бы я стал котом, чем бы я занимался весь день?', hint: 'И сколько раз за день требовал бы внимания? 🐈' },
  { id: 'zombies', text: 'Кто из нас первым выживет в зомби-апокалипсисе и почему?', hint: 'Обнимашки важны, но план побега тоже пригодится 🧟' },
  { id: 'hugs', text: 'Что я выберу: миллион рублей или пожизненный запас твоих обнимашек?', hint: 'А если обнимашки ещё и с поцелуями? 👀' },
  { id: 'aliens', text: 'Если меня похитят инопланетяне, по какой фразе ты сразу поймёшь, что это всё ещё я?', hint: 'Нужен пароль, который не подделает ни один пришелец 🛸' },
  { id: 'forever', text: 'Ты будешь со мной до конца жизни? Будешь со мной до последнего? В проблемах и блаженстве?', hint: 'Здесь я хочу услышать именно твои слова. Можно ответить так, как чувствуешь 💌' }
].map(Object.freeze));

export const ANSWER_IDS = Object.freeze(QUESTIONS.map(q => `${QUIZ_ID}-${q.id}`));
const noteIds = new Set([QUIZ_ID, ...ANSWER_IDS]);
export const isKnowledgeQuizNote = id => noteIds.has(id);
const codedError = code => Object.assign(new Error(code), { code });

export function validateAnswers(values) {
  if (!Array.isArray(values) || values.length !== QUESTIONS.length) throw codedError('incomplete');
  return values.map((value, index) => {
    if (typeof value !== 'string' || !value.trim()) throw codedError('incomplete');
    const answer = value.trim();
    if (answer.length > MAX_ANSWER || `${QUESTIONS[index].text}\n\n${answer}`.length > 590) throw codedError('too-long');
    return answer;
  });
}

// Keep the existing notebook's four-field schema and short text limit.
// The receipt and all fifteen answer documents commit together, or none do.
export async function submitOnce({ auth, db, doc, runTransaction, serverTimestamp, respondentUid, answers }) {
  const values = validateAnswers(answers);
  const sameUser = () => auth.currentUser?.uid === respondentUid;
  if (!sameUser()) throw codedError('not-respondent');
  const ref = id => doc(db, 'notebooks', 'zvezdochka-shared-notebook', 'notes', id);
  return runTransaction(db, async transaction => {
    if (!sameUser()) throw codedError('auth-changed');
    const receipt = await transaction.get(ref(QUIZ_ID));
    if (!sameUser()) throw codedError('auth-changed');
    if (receipt.exists()) return { created: false };
    const previous = await Promise.all(ANSWER_IDS.map(id => transaction.get(ref(id))));
    if (!sameUser()) throw codedError('auth-changed');
    if (previous.some(snapshot => snapshot.exists())) throw codedError('partial-submission');
    const stamp = serverTimestamp();
    const note = text => ({ text, done: false, createdAt: stamp, updatedAt: stamp });
    QUESTIONS.forEach((question, index) => transaction.set(ref(ANSWER_IDS[index]), note(`${question.text}\n\n${values[index]}`)));
    transaction.set(ref(QUIZ_ID), note(JSON.stringify({ kind: 'knowledgeQuiz', version: 1, respondentUid, answerCount: QUESTIONS.length })));
    return { created: true, answers: values };
  });
}

export function readSubmission(documents, respondentUid) {
  const docs = new Map(documents.map(document => [document.id, document.data()]));
  const receipt = docs.get(QUIZ_ID);
  if (!receipt) return ANSWER_IDS.some(id => docs.has(id)) ? { state: 'partial' } : { state: 'empty' };
  try {
    const meta = JSON.parse(receipt.text);
    if (meta.kind !== 'knowledgeQuiz' || meta.version !== 1 || meta.respondentUid !== respondentUid || meta.answerCount !== QUESTIONS.length) throw codedError('invalid');
    const answers = QUESTIONS.map((question, index) => {
      const text = docs.get(ANSWER_IDS[index])?.text;
      const prefix = `${question.text}\n\n`;
      if (typeof text !== 'string' || !text.startsWith(prefix)) throw codedError('invalid');
      return text.slice(prefix.length);
    });
    return { state: 'saved', answers: validateAnswers(answers), submittedAt: receipt.createdAt ?? null };
  } catch {
    // Existing data always locks this attempt. Never overwrite a damaged receipt.
    return { state: 'partial' };
  }
}
