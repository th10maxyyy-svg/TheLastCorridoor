# 🩸 The Last Corridoor - The Hardcore Nightmare Blueprint

Aapki demand par maine game ko ek aam puzzle se nikal kar ek **"Torturous Nightmare"** (Azab) bana diya hai. Is game ko khatam karna aam player ke bas ki baat nahi hogi. Ise is tarah lamba aur mushkil kiya gaya hai ke player ko bar bar asylum ke ek kone se dusre kone tak bhagna paray ga, aur har qadam par usey chhupna (Hide) parega.

---

## 👁️ The Enemy & Hiding Spots

**The Enemy (Brother Silas):** 
Yeh sirf aawaz nahi sunta, yeh aapki flashlight bhi dekhta hai. Agar aap deewar ke peechay khare hain lekin aapki saans (breathing) ki aawaz aa rahi hai (agar stamina kam hai), to yeh aapko dhoondh lega.

**Hiding Spots (Chhupne ki jagah):**
Game ko mushkil banane ke liye hum **`HidingSpot.cs`** ka istemal boht zyada karenge.
- **Cupboards (Almaari):** Wards aur HallDownstair mein rakhi hongi. Agar enemy aapke boht qareeb hai jab aap cupboard mein ghustay hain, to woh aapko andar se nikal kar maar dega. Aapko pehle uski line of sight se nikalna hoga.
- **Under Beds (Bistaron ke neechay):** DayRoom aur Wards mein.
- **Morgue Boxes:** Basement mein.

---

## 🗺️ The Torturous Progression (Extreme Difficulty Flow)

Yeh ek lamba aur thaka dene wala (exhausting) safar hai:

### Phase 1: The Bloody Awakening (Room: SeclusionRoomB)
- **Start:** Player **SeclusionRoomB** (Padded cell) mein uthta hai.
- **Escape:** Yahan koi chabi nahi hai. Player ko zameen par pary hue ganjay (rusty) spoon (chammach) se vent (hawa ka raasta) kholna parta hai. Isme time lagta hai, aur bahar corridor mein dushman chal raha hai. Player ko bar bar chhupna parta hai.
- **Vent Escape:** Player vent se nikal kar **GuardRoom** mein girta hai.

### Phase 2: The Generator Problem (Room: Basement & HallDownstair)
- **The Block:** Main darwaza (Main Exit) electronic hai! Usey kholne ke liye pehle Basement mein Generator on karna hoga. Lekin Elevator kharab hai (`BrokenElevator.cs`).
- **Action:** Player ko **HallDownstair** ki andheri seerhiyon (stairs) se neechay Basement jana parta hai.
- **The Catch:** Generator ka **Fuse** missing hai! Fuse bina generator on nahi hoga.

### Phase 3: The Fuse & The Fridge (Room: KitchenA & OfficeA)
- **Clue:** Basement mein note milta hai: *"Sparks are kept where the meat is cold."*
- **Action:** Player wapas Ground Floor par aata hai aur **KitchenA** mein jata hai. Kitchen mein Dushman ka pehra (patrol) sab se zyada hai. Player ko Kitchen ki table ke neechay chhupna (Hide) parta hai.
- **The Horror:** Jab woh **Fridge_DoorD** kholta hai, to wahan gosht ke dher mein ek choti si **Office Key** milti hai (Fuse nahi!).
- **Action:** Player ko wapas First Floor par **OfficeA** mein jana parta hai. OfficeA ke andar ek safe (tijori) hai. Safe ke andar se **Generator Fuse** milta hai.

### Phase 4: Powering Up (Room: Basement)
- Player Fuse le kar wapas **Basement** mein bhagta hai. (Dushman ab zyada aggressive ho chuka hai).
- Jaise hi woh Generator mein Fuse lagata hai aur usay On karta hai... **BOOM!** Boht shor hota hai. Poori building ki lights on ho jati hain.
- **The Nightmare:** Shor sun kar Dushman seedha Basement ki taraf bhagta hua aata hai! Player ke paas sirf 5 second hain wahan par rakhe **Morgue Box** ke andar (Hiding Spot) chupne ke liye! Agar wo na chupa, to game over.

### Phase 5: The Passcode of Blood (Room: DayRoom)
- Generator on hone se Main Door ko power mil gayi hai, lekin us par **4-Digit Keypad Lock** laga hai.
- Player ko wapas **DayRoom** aana parta hai jahan TV on ho chuka hai. TV ki screen par ajeeb si tasveerein aur khooni nishan aate hain jin se password (e.g. 7-3-9-1) banta hai. Yahan Dushman TV ki aawaz sun kar aa jata hai, player ko Sofay ke peeche ya Cupboard mein chupna parta hai.

### Phase 6: The Final Torment (Main Stairs)
- Player code le kar Main Exit par jata hai, code dalta hai. Darwaza khulne lagta hai (Isme 10 seconds lagte hain!).
- **The Climax:** Dushman alarm sun kar seedha Main Exit ki taraf bhaagta hai. Player ke paas hathiyaar nahi hai. Usay 10 seconds tak Dushman ko idhar udhar ghumana (kite karna) aur Wards mein chhupna parta hai jab tak ke darwaza poora khul na jaye.
- Jaise hi darwaza khulta hai, player bahar bhagta hai aur game khatam hoti hai.

---

## 🎯 Developer Action Items (Yeh game banane ke liye humain kya karna hoga):
1. **Hiding Spot Scripts:** Humain Wards aur Kitchen mein `HidingSpot.cs` objects (Cupboards, Beds) place karne honge.
2. **Key/Item Spawners:** Fuse, Spoon, aur Office Key ke 3D objects aur scripts lagane honge.
3. **Event Triggers:** Generator On karne par Dushman ko specifically wahan bulane ka (EnemyAI ko alert karne ka) event banana hoga.
4. **Time Delays:** Darwazay khulne aur vents open karne mein time delay lagana hoga taake player panic ho!

Yeh hai ek **Heavy Hardcore Storyline**! Agar player ek bhi qadam ghalat uthayega to game over. Kya aap ready hain is azab ko shuru karne ke liye?
