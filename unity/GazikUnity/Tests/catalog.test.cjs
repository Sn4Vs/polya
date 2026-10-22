// Baseline captured from the original website, independently of the Unity catalog.
const fs=require('node:fs'),path=require('node:path'),assert=require('node:assert/strict');
const read=file=>JSON.parse(fs.readFileSync(path.join(__dirname,file),'utf8'));
const baseline=read('original-catalog.json'),current=read('../Assets/Gazik/Resources/GazikCatalog.json');
for(const item of baseline.shop)assert.deepEqual(current.shop.find(x=>x.id===item.id),item);
assert.deepEqual(current.letters,baseline.letters);assert.deepEqual(current.stages,baseline.stages);
assert.equal(new Set(current.shop.map(x=>x.id)).size,current.shop.length);
console.log('PASS All 17 original products retain their exact IDs, text, categories and prices');
console.log('PASS All 20 original letters and five growth stage names match the website');
console.log('PASS Additional products use unique IDs');
