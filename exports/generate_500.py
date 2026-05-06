#!/usr/bin/env python3
"""Generate 500 synthetic VistA ZWR test patients with clinically coherent cross-references."""

import random
import os

random.seed(42)  # Reproducible output

OUT_DIR = os.path.join(os.path.dirname(__file__), "FiveHundred")
os.makedirs(OUT_DIR, exist_ok=True)

# ── Name pools ──────────────────────────────────────────────────────────────

LAST_NAMES = [
    "SMITH","JOHNSON","WILLIAMS","BROWN","JONES","GARCIA","MILLER","DAVIS",
    "RODRIGUEZ","MARTINEZ","HERNANDEZ","LOPEZ","GONZALEZ","WILSON","ANDERSON",
    "THOMAS","TAYLOR","MOORE","JACKSON","MARTIN","LEE","PEREZ","THOMPSON",
    "WHITE","HARRIS","SANCHEZ","CLARK","RAMIREZ","LEWIS","ROBINSON","WALKER",
    "YOUNG","ALLEN","KING","WRIGHT","SCOTT","TORRES","NGUYEN","HILL","FLORES",
    "GREEN","ADAMS","NELSON","BAKER","HALL","RIVERA","CAMPBELL","MITCHELL",
    "CARTER","ROBERTS","GOMEZ","PHILLIPS","EVANS","TURNER","DIAZ","PARKER",
    "CRUZ","EDWARDS","COLLINS","REYES","STEWART","MORRIS","MORALES","MURPHY",
    "COOK","ROGERS","GUTIERREZ","ORTIZ","MORGAN","COOPER","PETERSON","BAILEY",
    "REED","KELLY","HOWARD","RAMOS","KIM","COX","WARD","RICHARDSON","WATSON",
    "BROOKS","CHAVEZ","WOOD","JAMES","BENNETT","GRAY","MENDOZA","RUIZ",
    "HUGHES","PRICE","ALVAREZ","CASTILLO","SANDERS","PATEL","MYERS","LONG",
    "ROSS","FOSTER","JIMENEZ","POWELL","JENKINS","PERRY","RUSSELL","SULLIVAN",
    "BELL","COLEMAN","BUTLER","HENDERSON","BARNES","GONZALES","FISHER",
    "VASQUEZ","SIMMONS","GRAHAM","MCDONALD","SPENCER","WAGNER","HUNT",
    "FRANKLIN","CUNNINGHAM","LYNCH","BISHOP","RILEY","BARRETT","HART",
    "MASON","FORD","WASHINGTON","HOFFMAN","SNYDER","MALDONADO","DUNN",
    "FREEMAN","BURNS","DRAKE","COLE","WEST","JORDAN","OWENS","REYNOLDS",
    "WARREN","DIXON","HARRISON","GIBSON","KNIGHT","DUNCAN","CARR","HAWKINS",
    "STONE","BLACK","ARMSTRONG","PIERCE","ARNOLD","RAY","WEAVER","OLSON",
    "WEBB","FIELDS","SCHWARTZ","GRANT","TODD","SILVA","MANN","CHEN","VARGAS",
    "AGUILAR","MEDINA","SANTOS","DELEON","PENA","SERRANO","VEGA","ESTRADA",
    "ROJAS","GUERRERO","ACOSTA","SOTO","BAUTISTA","PADILLA","DOMINGUEZ",
    "CONTRERAS","CORTEZ","MONTOYA","OCHOA","FUENTES","SALAZAR","CARDENAS",
    "TRUJILLO","HERRERA","ESPINOZA","GALLEGOS","SANDOVAL","MONTES","MEJIA",
    "VELASQUEZ","NUNEZ","RIOS","MERCADO","OROZCO","CORONA","VILLANUEVA",
]

MALE_FIRST = [
    "JOHN","ROBERT","MICHAEL","DAVID","WILLIAM","RICHARD","CHARLES","THOMAS",
    "CHRISTOPHER","DANIEL","MATTHEW","ANTHONY","MARK","DONALD","STEVEN",
    "ANDREW","KENNETH","JOSHUA","GEORGE","EDWARD","BRIAN","RONALD","TIMOTHY",
    "JASON","JEFFREY","RYAN","JACOB","NICHOLAS","GARY","ERIC","STEPHEN",
    "JONATHAN","LARRY","KEVIN","FRANK","SCOTT","RAYMOND","GREGORY","SAMUEL",
    "BENJAMIN","PATRICK","JACK","DENNIS","JERRY","ALEXANDER","HENRY","CARL",
    "ARTHUR","PETER","ALBERT","LAWRENCE","JESSE","WAYNE","ROGER","BRUCE",
    "RALPH","ROY","EUGENE","RUSSELL","LOUIS","PHILIP","HARRY","VINCENT",
    "BOBBY","DYLAN","RANDY","HOWARD","EUGENE","TRAVIS","DUSTIN","SERGIO",
    "CARLOS","MIGUEL","JOSE","LUIS","JUAN","RICARDO","MARCOS","RAFAEL",
]

FEMALE_FIRST = [
    "MARY","PATRICIA","JENNIFER","LINDA","BARBARA","ELIZABETH","SUSAN",
    "JESSICA","SARAH","KAREN","NANCY","LISA","BETTY","MARGARET","SANDRA",
    "DOROTHY","DONNA","CAROL","RUTH","SHARON","HELEN","DEBORAH","DEBRA",
    "LAURA","DIANE","ALICE","KATHLEEN","VIRGINIA","FRANCES","TERESA",
    "ANN","JEAN","ABIGAIL","JOYCE","MARIE","DENISE","CATHERINE","CHRISTINE",
    "BEVERLY","THERESA","JANET","JULIA","GRACE","AMBER","TIFFANY","NICOLE",
    "BRENDA","TAMMY","RACHEL","REBECCA","VICTORIA","GLORIA","ROSA","MARIA",
    "CARMEN","ELENA","SILVIA","GABRIELA","ADRIANA","FERNANDA","VALERIA",
    "CONNIE","LILLIAN","ROBIN","EMILY","ASHLEY","MEGAN","MICHELLE","KIMBERLY",
    "ANGELA","AMY","STEPHANIE","HEATHER","CHRISTINA","ANNA","KELLY","MARTHA",
]

STREETS = [
    "OAK","MAPLE","ELM","PINE","BIRCH","CEDAR","WALNUT","SPRUCE","HICKORY",
    "MAGNOLIA","POPLAR","SYCAMORE","CHESTNUT","BEECH","REDWOOD","CYPRESS",
    "PALM","CHERRY","DOGWOOD","ASPEN","JUNIPER","LOCUST","ALDER","HAWTHORN",
    "COTTONWOOD","MULBERRY","BASSWOOD","CATALPA","WILLOW","LINDEN","HEMLOCK",
    "PECAN","IRONWOOD","BOXWOOD","LAUREL","MYRTLE","OLIVE","HOLLY","IVY",
    "LILAC","MARIGOLD","PRIMROSE","VERBENA","ASTER","ZINNIA","GARDENIA",
    "PETUNIA","DAHLIA","ORCHID","IRIS","ACACIA","SEQUOIA","MESQUITE","SAGE",
    "ROSEWOOD","BAMBOO","JASMINE","AZALEA","CAMELLIA","FERN","HEATHER",
    "CLOVER","THISTLE","WISTERIA","LARKSPUR","COLUMBINE","FOXGLOVE","VIOLET",
]

STREET_TYPES = ["STREET","AVENUE","DRIVE","ROAD","LANE","COURT","WAY","BOULEVARD","PLACE","CIRCLE"]

CITIES_STATES_ZIPS = [
    ("RICHMOND","VA","23220"),("DALLAS","TX","75201"),("SEATTLE","WA","98101"),
    ("CHICAGO","IL","60601"),("PHOENIX","AZ","85001"),("SAN ANTONIO","TX","78201"),
    ("DENVER","CO","80201"),("PORTLAND","OR","97201"),("MIAMI","FL","33101"),
    ("ATLANTA","GA","30301"),("MINNEAPOLIS","MN","55401"),("BOSTON","MA","02101"),
    ("TAMPA","FL","33601"),("NASHVILLE","TN","37201"),("SALT LAKE CITY","UT","84101"),
    ("COLUMBUS","OH","43201"),("CHARLOTTE","NC","28201"),("JACKSONVILLE","FL","32201"),
    ("SAN FRANCISCO","CA","94101"),("INDIANAPOLIS","IN","46201"),("AUSTIN","TX","78701"),
    ("MILWAUKEE","WI","53201"),("PITTSBURGH","PA","15201"),("ALBUQUERQUE","NM","87101"),
    ("TUCSON","AZ","85701"),("FRESNO","CA","93701"),("SACRAMENTO","CA","95801"),
    ("OMAHA","NE","68101"),("RALEIGH","NC","27601"),("LOUISVILLE","KY","40201"),
    ("VIRGINIA BEACH","VA","23451"),("OKLAHOMA CITY","OK","73101"),("HARTFORD","CT","06101"),
    ("KANSAS CITY","MO","64101"),("MEMPHIS","TN","38101"),("BUFFALO","NY","14201"),
    ("ST LOUIS","MO","63101"),("NEW ORLEANS","LA","70101"),("CLEVELAND","OH","44101"),
    ("LAS VEGAS","NV","89101"),("BALTIMORE","MD","21201"),("DETROIT","MI","48201"),
    ("EL PASO","TX","79901"),("HONOLULU","HI","96801"),("ANCHORAGE","AK","99501"),
    ("SAN DIEGO","CA","92101"),("BOISE","ID","83701"),("COLUMBIA","SC","29201"),
    ("LITTLE ROCK","AR","72201"),("BIRMINGHAM","AL","35201"),("TULSA","OK","74101"),
    ("WICHITA","KS","67201"),("SPOKANE","WA","99201"),("TACOMA","WA","98401"),
    ("HUNTSVILLE","AL","35801"),("FAYETTEVILLE","NC","28301"),("KNOXVILLE","TN","37901"),
    ("MOBILE","AL","36601"),("SAVANNAH","GA","31401"),("NORFOLK","VA","23501"),
    ("DES MOINES","IA","50301"),("DAYTON","OH","45401"),("SIOUX FALLS","SD","57101"),
    ("CHARLESTON","SC","29401"),("SHREVEPORT","LA","71101"),("LUBBOCK","TX","79401"),
    ("LAREDO","TX","78040"),("AMARILLO","TX","79101"),("CORPUS CHRISTI","TX","78401"),
    ("RENO","NV","89501"),("EUGENE","OR","97401"),("FORT WAYNE","IN","46801"),
    ("MADISON","WI","53701"),("GREENVILLE","SC","29601"),("LEXINGTON","KY","40501"),
]

AREA_CODES = [
    "804","214","206","312","602","210","303","503","305","404","612","617",
    "813","615","801","614","704","904","415","317","512","414","412","505",
    "520","559","916","402","919","502","757","405","860","816","901","716",
    "314","504","216","702","410","313","915","808","907","619","208","803",
    "501","205","918","316","509","253","256","910","865","251","912","757",
    "515","937","605","843","318","806","956","806","361","775","541","260",
    "608","864","859",
]

BRANCHES = ["ARMY","NAVY","AIR FORCE","MARINES","COAST GUARD"]
EMERG_RELS = ["SPOUSE","SON","DAUGHTER","BROTHER","SISTER","MOTHER","FATHER","FRIEND","GRANDSON","GRANDDAUGHTER"]

# ── Clinical data pools ─────────────────────────────────────────────────────

ALLERGY_DRUGS = [
    ("PENICILLIN","Drug","3;PSDRUG("),("SULFA DRUGS","Drug","15;PSDRUG("),
    ("ASPIRIN","Drug","8;PSDRUG("),("CODEINE","Drug","22;PSDRUG("),
    ("MORPHINE","Drug","30;PSDRUG("),("ERYTHROMYCIN","Drug","12;PSDRUG("),
    ("TETRACYCLINE","Drug","18;PSDRUG("),("METFORMIN","Drug","45;PSDRUG("),
    ("AMOXICILLIN","Drug","5;PSDRUG("),("TRAMADOL","Drug","50;PSDRUG("),
    ("CIPROFLOXACIN","Drug","28;PSDRUG("),("HYDROCODONE","Drug","35;PSDRUG("),
    ("LISINOPRIL","Drug","40;PSDRUG("),("VANCOMYCIN","Drug","55;PSDRUG("),
    ("GABAPENTIN","Drug","60;PSDRUG("),("METOPROLOL","Drug","42;PSDRUG("),
    ("NAPROXEN","Drug","9;PSDRUG("),("OXYCODONE","Drug","33;PSDRUG("),
    ("CLINDAMYCIN","Drug","25;PSDRUG("),("IBUPROFEN","Drug","10;PSDRUG("),
    ("METHOTREXATE","Drug","48;PSDRUG("),("WARFARIN","Drug","65;PSDRUG("),
    ("LIDOCAINE","Drug","38;PSDRUG("),
]
ALLERGY_CLASSES = [
    ("NSAIDS","Drug Class","NSAIDS"),("ACE INHIBITORS","Drug Class","ACE INHIBITORS"),
    ("STATINS","Drug Class","STATINS"),("CEPHALOSPORINS","Drug Class","CEPHALOSPORINS"),
    ("BENZODIAZEPINES","Drug Class","BENZODIAZEPINES"),("FLUOROQUINOLONES","Drug Class","FLUOROQUINOLONES"),
]
ALLERGY_OTHER = [
    ("LATEX","Other","LATEX"),("DUST","Other","DUST"),("BEE STINGS","Other","BEE VENOM"),
    ("CONTRAST DYE","Other","CONTRAST MEDIA"),("ADHESIVE TAPE","Other","ADHESIVE"),
    ("TREE POLLEN","Other","TREE POLLEN"),("CATS","Other","CAT DANDER"),
    ("MOLD","Other","MOLD SPORES"),("PEANUTS","Food","PEANUTS"),
    ("SHELLFISH","Food","SHELLFISH"),("EGGS","Food","EGGS"),("MILK","Food","MILK PRODUCTS"),
]
ALLERGY_REACTIONS = [
    "HIVES","RASH","NAUSEA","VOMITING","SHORTNESS OF BREATH","ITCHING",
    "ANAPHYLAXIS","GI UPSET","DIARRHEA","SWELLING","THROAT SWELLING",
    "RESPIRATORY DISTRESS","DIZZINESS","DROWSINESS","ANGIOEDEMA","COUGH",
    "STOMACH PAIN","SKIN IRRITATION","PHOTOSENSITIVITY","BRADYCARDIA","FATIGUE",
    "GI BLEEDING","MOUTH SORES","SEIZURE","PALPITATIONS","BRUISING",
    "CONGESTION","SNEEZING","WATERY EYES","CONTACT DERMATITIS",
    "RED MAN SYNDROME","C. DIFF COLITIS","TENDON PAIN","TENDON RUPTURE",
]
SEVERITIES = ["MILD","MODERATE","SEVERE"]

# Clinical profiles: (condition_name, icd10, is_chronic, sc_probability)
PROBLEM_POOL = [
    ("ESSENTIAL HYPERTENSION","I10","CHRONIC",0.3),
    ("TYPE 2 DIABETES MELLITUS","E11.9","CHRONIC",0.1),
    ("POST-TRAUMATIC STRESS DISORDER","F43.10","CHRONIC",0.8),
    ("MAJOR DEPRESSIVE DISORDER","F33.0","CHRONIC",0.3),
    ("GENERALIZED ANXIETY DISORDER","F41.1","CHRONIC",0.2),
    ("CHRONIC LOW BACK PAIN","M54.5","CHRONIC",0.5),
    ("LUMBAR DEGENERATIVE DISC DISEASE","M51.36","CHRONIC",0.4),
    ("CHRONIC KNEE PAIN","M25.569","CHRONIC",0.5),
    ("TINNITUS","H93.19","CHRONIC",0.8),
    ("BILATERAL HEARING LOSS","H91.93","CHRONIC",0.7),
    ("OBSTRUCTIVE SLEEP APNEA","G47.33","CHRONIC",0.1),
    ("HYPOTHYROIDISM","E03.9","CHRONIC",0.05),
    ("HYPERLIPIDEMIA","E78.5","CHRONIC",0.1),
    ("GASTROESOPHAGEAL REFLUX DISEASE","K21.0","CHRONIC",0.05),
    ("CORONARY ARTERY DISEASE","I25.10","CHRONIC",0.2),
    ("CONGESTIVE HEART FAILURE","I50.9","CHRONIC",0.1),
    ("ATRIAL FIBRILLATION","I48.91","CHRONIC",0.05),
    ("CHRONIC KIDNEY DISEASE STAGE 3","N18.3","CHRONIC",0.05),
    ("ASTHMA","J45.20","CHRONIC",0.1),
    ("COPD","J44.1","CHRONIC",0.3),
    ("OSTEOARTHRITIS OF BOTH KNEES","M17.0","CHRONIC",0.1),
    ("RHEUMATOID ARTHRITIS","M06.9","CHRONIC",0.05),
    ("CHRONIC MIGRAINE","G43.709","CHRONIC",0.3),
    ("PERIPHERAL NEUROPATHY","G62.9","CHRONIC",0.4),
    ("IRRITABLE BOWEL SYNDROME","K58.9","CHRONIC",0.05),
    ("TRAUMATIC BRAIN INJURY RESIDUALS","S06.9XAS","CHRONIC",0.9),
    ("ADJUSTMENT DISORDER","F43.20","CHRONIC",0.2),
    ("OSTEOPOROSIS","M81.0","CHRONIC",0.05),
    ("FIBROMYALGIA","M79.7","CHRONIC",0.1),
    ("CHRONIC HEPATITIS C","B18.2","CHRONIC",0.3),
    ("BENIGN PROSTATIC HYPERPLASIA","N40.0","CHRONIC",0.05),
    ("IRON DEFICIENCY ANEMIA","D50.9","CHRONIC",0.05),
    ("GOUT","M10.9","CHRONIC",0.1),
    ("CERVICAL SPONDYLOSIS","M47.812","CHRONIC",0.3),
    ("CHRONIC INSOMNIA","G47.00","CHRONIC",0.2),
    ("CHRONIC PAIN SYNDROME","G89.29","CHRONIC",0.5),
    ("PERIPHERAL VASCULAR DISEASE","I73.9","CHRONIC",0.1),
    ("PARKINSON DISEASE","G20","CHRONIC",0.05),
    ("ANXIETY DISORDER","F41.9","CHRONIC",0.2),
    ("CHRONIC FATIGUE SYNDROME","R53.82","CHRONIC",0.1),
]

# Medication pool: (drug_name, dose, route, schedule, sig, days, qty, refills)
MED_POOL = [
    ("LISINOPRIL 10MG TAB","10MG","ORAL","DAILY","TAKE ONE TABLET BY MOUTH DAILY",90,90,3),
    ("LISINOPRIL 20MG TAB","20MG","ORAL","DAILY","TAKE ONE TABLET BY MOUTH DAILY",90,90,3),
    ("METFORMIN 500MG TAB","500MG","ORAL","BID","TAKE ONE TABLET BY MOUTH TWICE DAILY",90,180,3),
    ("METFORMIN 1000MG TAB","1000MG","ORAL","BID","TAKE ONE TABLET BY MOUTH TWICE DAILY",90,180,3),
    ("SERTRALINE 50MG TAB","50MG","ORAL","DAILY","TAKE ONE TABLET BY MOUTH DAILY",90,90,3),
    ("SERTRALINE 100MG TAB","100MG","ORAL","DAILY","TAKE ONE TABLET BY MOUTH DAILY",90,90,3),
    ("AMLODIPINE 5MG TAB","5MG","ORAL","DAILY","TAKE ONE TABLET BY MOUTH DAILY",90,90,3),
    ("AMLODIPINE 10MG TAB","10MG","ORAL","DAILY","TAKE ONE TABLET BY MOUTH DAILY",90,90,3),
    ("ATORVASTATIN 20MG TAB","20MG","ORAL","QHS","TAKE ONE TABLET BY MOUTH AT BEDTIME",90,90,3),
    ("ATORVASTATIN 40MG TAB","40MG","ORAL","QHS","TAKE ONE TABLET BY MOUTH AT BEDTIME",90,90,3),
    ("OMEPRAZOLE 20MG CAP","20MG","ORAL","DAILY","TAKE ONE CAPSULE BY MOUTH DAILY BEFORE BREAKFAST",90,90,3),
    ("GABAPENTIN 300MG CAP","300MG","ORAL","TID","TAKE ONE CAPSULE BY MOUTH THREE TIMES DAILY",90,270,3),
    ("GABAPENTIN 600MG CAP","600MG","ORAL","TID","TAKE ONE CAPSULE BY MOUTH THREE TIMES DAILY",90,270,3),
    ("PRAZOSIN 1MG CAP","1MG","ORAL","QHS","TAKE ONE CAPSULE BY MOUTH AT BEDTIME",90,90,3),
    ("PRAZOSIN 2MG CAP","2MG","ORAL","QHS","TAKE ONE CAPSULE BY MOUTH AT BEDTIME",90,90,3),
    ("ALBUTEROL INHALER","2 PUFFS","INHALATION","Q4-6H PRN","INHALE 2 PUFFS EVERY 4 TO 6 HOURS AS NEEDED",30,1,5),
    ("SUMATRIPTAN 50MG TAB","50MG","ORAL","PRN","TAKE ONE TABLET AT ONSET OF MIGRAINE",30,9,5),
    ("FUROSEMIDE 40MG TAB","40MG","ORAL","BID","TAKE ONE TABLET BY MOUTH TWICE DAILY",90,180,3),
    ("CARVEDILOL 12.5MG TAB","12.5MG","ORAL","BID","TAKE ONE TABLET BY MOUTH TWICE DAILY",90,180,3),
    ("WARFARIN 5MG TAB","5MG","ORAL","DAILY","TAKE ONE TABLET BY MOUTH DAILY",90,90,3),
    ("DULOXETINE 60MG CAP","60MG","ORAL","DAILY","TAKE ONE CAPSULE BY MOUTH DAILY",90,90,3),
    ("TIOTROPIUM INHALER","18MCG","INHALATION","DAILY","INHALE ONE CAPSULE DAILY USING HANDIHALER",30,30,5),
    ("METHOTREXATE 7.5MG TAB","7.5MG","ORAL","WEEKLY","TAKE ONE TABLET BY MOUTH ONCE WEEKLY",30,4,3),
    ("BUSPIRONE 10MG TAB","10MG","ORAL","TID","TAKE ONE TABLET BY MOUTH THREE TIMES DAILY",90,270,3),
    ("TAMOXIFEN 20MG TAB","20MG","ORAL","DAILY","TAKE ONE TABLET BY MOUTH DAILY",90,90,3),
    ("HYDROXYCHLOROQUINE 200MG TAB","200MG","ORAL","BID","TAKE ONE TABLET BY MOUTH TWICE DAILY",90,180,3),
    ("NAPROXEN 500MG TAB","500MG","ORAL","BID","TAKE ONE TABLET BY MOUTH TWICE DAILY WITH FOOD",90,180,3),
    ("TOPIRAMATE 25MG TAB","25MG","ORAL","BID","TAKE ONE TABLET BY MOUTH TWICE DAILY",90,180,3),
    ("COLCHICINE 0.6MG TAB","0.6MG","ORAL","DAILY","TAKE ONE TABLET BY MOUTH DAILY",90,90,3),
    ("LEVOTHYROXINE 75MCG TAB","75MCG","ORAL","DAILY","TAKE ONE TABLET BY MOUTH EACH MORNING ON EMPTY STOMACH",90,90,3),
    ("TAMSULOSIN 0.4MG CAP","0.4MG","ORAL","DAILY","TAKE ONE CAPSULE BY MOUTH DAILY 30 MINUTES AFTER SAME MEAL",90,90,3),
    ("DONEPEZIL 10MG TAB","10MG","ORAL","QHS","TAKE ONE TABLET BY MOUTH AT BEDTIME",90,90,3),
    ("CARBIDOPA-LEVODOPA 25-100MG TAB","25-100MG","ORAL","TID","TAKE ONE TABLET BY MOUTH THREE TIMES DAILY",90,270,3),
    ("IBUPROFEN 400MG TAB","400MG","ORAL","TID PRN","TAKE ONE TABLET BY MOUTH THREE TIMES DAILY AS NEEDED",90,270,3),
    ("ACETAMINOPHEN 500MG TAB","500MG","ORAL","Q6H PRN","TAKE ONE TABLET BY MOUTH EVERY 6 HOURS AS NEEDED FOR PAIN",90,120,3),
    ("DICYCLOMINE 20MG CAP","20MG","ORAL","QID","TAKE ONE CAPSULE BY MOUTH FOUR TIMES DAILY",90,360,3),
    ("FERROUS SULFATE 325MG TAB","325MG","ORAL","DAILY","TAKE ONE TABLET BY MOUTH DAILY WITH FOOD",90,90,3),
    ("OXYCODONE 5MG TAB","5MG","ORAL","Q6H","TAKE ONE TABLET BY MOUTH EVERY 6 HOURS",30,120,0),
    ("ALENDRONATE 70MG TAB","70MG","ORAL","WEEKLY","TAKE ONE TABLET BY MOUTH ONCE WEEKLY ON EMPTY STOMACH",30,4,3),
    ("MELOXICAM 15MG TAB","15MG","ORAL","DAILY","TAKE ONE TABLET BY MOUTH DAILY WITH FOOD",90,90,3),
    ("LOSARTAN 50MG TAB","50MG","ORAL","DAILY","TAKE ONE TABLET BY MOUTH DAILY",90,90,3),
    ("PANTOPRAZOLE 40MG TAB","40MG","ORAL","DAILY","TAKE ONE TABLET BY MOUTH DAILY BEFORE BREAKFAST",90,90,3),
    ("TRAZODONE 50MG TAB","50MG","ORAL","QHS","TAKE ONE TABLET BY MOUTH AT BEDTIME",90,90,3),
    ("MONTELUKAST 10MG TAB","10MG","ORAL","QHS","TAKE ONE TABLET BY MOUTH AT BEDTIME",90,90,3),
    ("CLOPIDOGREL 75MG TAB","75MG","ORAL","DAILY","TAKE ONE TABLET BY MOUTH DAILY",90,90,3),
    ("METOPROLOL 25MG TAB","25MG","ORAL","BID","TAKE ONE TABLET BY MOUTH TWICE DAILY",90,180,3),
    ("METOPROLOL 50MG TAB","50MG","ORAL","BID","TAKE ONE TABLET BY MOUTH TWICE DAILY",90,180,3),
    ("SIMVASTATIN 20MG TAB","20MG","ORAL","QHS","TAKE ONE TABLET BY MOUTH AT BEDTIME",90,90,3),
    ("FLUOXETINE 20MG CAP","20MG","ORAL","DAILY","TAKE ONE CAPSULE BY MOUTH DAILY",90,90,3),
    ("CITALOPRAM 20MG TAB","20MG","ORAL","DAILY","TAKE ONE TABLET BY MOUTH DAILY",90,90,3),
]

# Lab test pools
LAB_TESTS = {
    "GLUCOSE":     ("mg/dL","70","100", lambda: random.choice([random.randint(70,100), random.randint(101,180)])),
    "HBA1C":       ("%","4.0","5.6",    lambda: round(random.uniform(5.0, 9.5), 1)),
    "CREATININE":  ("mg/dL","0.7","1.3", lambda: round(random.uniform(0.6, 2.5), 1)),
    "BUN":         ("mg/dL","7","20",    lambda: random.randint(8, 35)),
    "POTASSIUM":   ("mEq/L","3.5","5.0", lambda: round(random.uniform(3.3, 5.5), 1)),
    "SODIUM":      ("mEq/L","136","145", lambda: random.randint(130, 148)),
    "CBC WBC":     ("K/cmm","4.5","11.0",lambda: round(random.uniform(3.5, 12.0), 1)),
    "HEMOGLOBIN":  ("g/dL","12.0","16.0",lambda: round(random.uniform(9.0, 17.5), 1)),
    "PLATELET COUNT":("K/cmm","150","400",lambda: random.randint(100, 450)),
    "TOTAL CHOLESTEROL":("mg/dL","0","200",lambda: random.randint(140, 280)),
    "LDL":         ("mg/dL","0","100",   lambda: random.randint(60, 180)),
    "TRIGLYCERIDES":("mg/dL","0","150",  lambda: random.randint(80, 300)),
    "ALT":         ("U/L","7","56",      lambda: random.randint(10, 90)),
    "AST":         ("U/L","10","40",     lambda: random.randint(10, 85)),
    "TSH":         ("mIU/L","0.4","4.0", lambda: round(random.uniform(0.3, 8.0), 1)),
    "INR":         ("ratio","2.0","3.0", lambda: round(random.uniform(1.5, 3.5), 1)),
    "BNP":         ("pg/mL","0","100",   lambda: random.choice([random.randint(20,100), random.randint(200,900)])),
    "PSA":         ("ng/mL","0","4.0",   lambda: round(random.uniform(0.5, 12.0), 1)),
    "ESR":         ("mm/hr","0","20",    lambda: random.randint(5, 55)),
    "CRP":         ("mg/L","0","1.0",    lambda: round(random.uniform(0.2, 5.0), 1)),
    "VITAMIN D":   ("ng/mL","30","100",  lambda: random.randint(12, 65)),
    "CALCIUM":     ("mg/dL","8.5","10.5",lambda: round(random.uniform(8.0, 11.0), 1)),
    "URIC ACID":   ("mg/dL","3.0","7.0", lambda: round(random.uniform(3.0, 10.0), 1)),
    "GFR":         ("mL/min","60","120", lambda: random.randint(15, 120)),
    "IRON":        ("mcg/dL","60","170", lambda: random.randint(25, 200)),
    "FERRITIN":    ("ng/mL","12","150",  lambda: random.randint(8, 200)),
    "ALBUMIN":     ("g/dL","3.5","5.0",  lambda: round(random.uniform(2.5, 5.0), 1)),
    "FREE T4":     ("ng/dL","0.8","1.8", lambda: round(random.uniform(0.6, 2.0), 1)),
}

VITAL_TYPES = ["BLOOD PRESSURE","PULSE","TEMPERATURE","RESPIRATION","WEIGHT","HEIGHT","PULSE OXIMETRY","PAIN"]
PAIN_LOCATIONS = ["HEAD","KNEE","BACK","JOINTS","FEET","NECK","SHOULDER","HIP","CHEST","ABDOMEN"]

ORDER_TYPES_PHARMACY = [
    "LISINOPRIL 10MG TAB","LISINOPRIL 20MG TAB","METFORMIN 500MG TAB","METFORMIN 1000MG TAB",
    "SERTRALINE 50MG TAB","SERTRALINE 100MG TAB","AMLODIPINE 5MG TAB","AMLODIPINE 10MG TAB",
    "ATORVASTATIN 20MG TAB","GABAPENTIN 300MG CAP","OMEPRAZOLE 20MG CAP","PRAZOSIN 1MG CAP",
    "ALBUTEROL INHALER","SUMATRIPTAN 50MG TAB","FUROSEMIDE 40MG TAB","CARVEDILOL 12.5MG TAB",
    "WARFARIN 5MG TAB","DULOXETINE 60MG CAP","TIOTROPIUM INHALER","BUSPIRONE 10MG TAB",
    "ACETAMINOPHEN 500MG TAB","NAPROXEN 500MG TAB","TOPIRAMATE 25MG TAB","HYDROXYCHLOROQUINE 200MG TAB",
    "LOSARTAN 50MG TAB","METOPROLOL 25MG TAB","CLOPIDOGREL 75MG TAB","PANTOPRAZOLE 40MG TAB",
]
ORDER_TYPES_LAB = [
    "COMPREHENSIVE METABOLIC PANEL","BASIC METABOLIC PANEL","CBC WITH DIFFERENTIAL",
    "LIPID PANEL","HEMOGLOBIN A1C","THYROID PANEL","HEPATIC FUNCTION PANEL",
    "URINALYSIS","PSA","INR","BNP","VITAMIN D LEVEL",
]
ORDER_TYPES_CONSULT = [
    "PSYCHOLOGY CONSULT","ORTHOPEDIC CONSULT","CARDIOLOGY CONSULT","PULMONARY CONSULT",
    "NEUROLOGY CONSULT","PAIN MANAGEMENT CONSULT","ENDOCRINOLOGY CONSULT",
]

CONSULT_SERVICES = [
    "CARDIOLOGY","ORTHOPEDICS","PSYCHOLOGY","PULMONARY","RHEUMATOLOGY","NEUROLOGY",
    "ONCOLOGY","NEPHROLOGY","HEPATOLOGY","PAIN MANAGEMENT","UROLOGY","GERIATRICS",
    "ENDOCRINOLOGY","SLEEP MEDICINE","GASTROENTEROLOGY","DERMATOLOGY",
    "PHYSICAL THERAPY","AUDIOLOGY","OPHTHALMOLOGY","PODIATRY","NUTRITION","SOCIAL WORK",
]
CONSULT_REASONS = {
    "CARDIOLOGY": ["Evaluate for coronary artery disease.","CHF exacerbation management.","Arrhythmia evaluation."],
    "ORTHOPEDICS": ["Evaluate joint pain and limited ROM.","Post-injury evaluation.","Arthritis management."],
    "PSYCHOLOGY": ["PTSD evaluation and treatment.","Depression management.","Anxiety assessment."],
    "PULMONARY": ["COPD management and rehab referral.","Dyspnea evaluation.","Pulmonary function testing."],
    "RHEUMATOLOGY": ["Evaluate joint swelling and elevated ESR.","Autoimmune workup.","RA management."],
    "NEUROLOGY": ["TBI follow-up evaluation.","Seizure evaluation.","Neuropathy assessment."],
    "ONCOLOGY": ["Cancer follow-up and surveillance.","New mass evaluation.","Treatment planning."],
    "NEPHROLOGY": ["CKD management and monitoring.","Declining GFR evaluation.","Electrolyte management."],
    "HEPATOLOGY": ["Hepatitis C treatment evaluation.","Cirrhosis management.","Liver function decline."],
    "PAIN MANAGEMENT": ["Chronic pain refractory to treatment.","Multimodal pain approach.","Medication review."],
    "UROLOGY": ["Elevated PSA evaluation.","BPH management.","Urinary symptoms."],
    "GERIATRICS": ["Cognitive decline assessment.","Falls risk evaluation.","Comprehensive geriatric assessment."],
    "ENDOCRINOLOGY": ["Uncontrolled diabetes management.","Thyroid disorder evaluation.","Metabolic syndrome."],
    "SLEEP MEDICINE": ["Suspected sleep apnea.","CPAP titration.","Insomnia evaluation."],
    "GASTROENTEROLOGY": ["IBS management.","GI bleeding evaluation.","GERD refractory to PPI."],
    "DERMATOLOGY": ["Suspicious skin lesion evaluation.","Chronic rash assessment.","Skin cancer screening."],
    "PHYSICAL THERAPY": ["Post-surgical rehabilitation.","Core strengthening for back pain.","ROM restoration."],
    "AUDIOLOGY": ["Hearing loss evaluation.","Hearing aid fitting.","Tinnitus assessment."],
    "OPHTHALMOLOGY": ["Diabetic retinopathy screening.","Vision changes evaluation.","Glaucoma screening."],
    "PODIATRY": ["Diabetic foot exam.","Foot pain evaluation.","Nail care."],
    "NUTRITION": ["Diabetes diet counseling.","Weight management.","Renal diet education."],
    "SOCIAL WORK": ["Benefits counseling.","Housing assistance.","Caregiver support."],
}

DOCTOR_NAMES = [f"DR {ln}" for ln in LAST_NAMES[:80]]

SURGERY_PROCEDURES = [
    ("LEFT KNEE ARTHROPLASTY","GENERAL","ORTHOPEDICS","SEVERE OSTEOARTHRITIS LEFT KNEE",
     ["Procedure: Total left knee arthroplasty","Findings: Severe tricompartmental osteoarthritis","Implant: Stryker Triathlon cemented TKA","EBL: 250ml. No complications."]),
    ("RIGHT KNEE ARTHROPLASTY","GENERAL","ORTHOPEDICS","SEVERE OSTEOARTHRITIS RIGHT KNEE",
     ["Procedure: Total right knee arthroplasty","Findings: Severe medial compartment osteoarthritis","Implant: Smith & Nephew Genesis II","EBL: 200ml. No complications."]),
    ("RIGHT TOTAL HIP ARTHROPLASTY","SPINAL","ORTHOPEDICS","SEVERE OSTEOARTHRITIS RIGHT HIP",
     ["Procedure: Posterior approach right total hip arthroplasty","Implant: Stryker Accolade II stem, Trident cup","EBL: 300ml. Hip stable in all planes."]),
    ("LEFT TOTAL HIP ARTHROPLASTY","SPINAL","ORTHOPEDICS","SEVERE OSTEOARTHRITIS LEFT HIP",
     ["Procedure: Anterior approach left total hip arthroplasty","Implant: DePuy Pinnacle cup, Corail stem","EBL: 275ml. No complications."]),
    ("CORONARY ARTERY BYPASS GRAFT X3","GENERAL","CARDIAC SURGERY","THREE-VESSEL CORONARY ARTERY DISEASE",
     ["Procedure: CABG x3 (LIMA to LAD, SVG to RCA, SVG to OM1)","CPB time: 95 minutes. Cross-clamp: 62 minutes.","EBL: 600ml. Transferred to SICU in stable condition."]),
    ("LEFT BREAST LUMPECTOMY","LOCAL WITH SEDATION","GENERAL SURGERY","BREAST CARCINOMA LEFT",
     ["Procedure: Left breast lumpectomy with sentinel lymph node biopsy","Findings: 1.8cm mass at 2 o'clock position","Sentinel node negative. Margins clear. EBL: 50ml."]),
    ("LAPAROSCOPIC CHOLECYSTECTOMY","GENERAL","GENERAL SURGERY","SYMPTOMATIC CHOLELITHIASIS",
     ["Procedure: Laparoscopic cholecystectomy","Findings: Chronically inflamed gallbladder with multiple stones","EBL: 25ml. No complications. Same-day discharge."]),
    ("RIGHT ROTATOR CUFF REPAIR","GENERAL","ORTHOPEDICS","FULL-THICKNESS ROTATOR CUFF TEAR",
     ["Procedure: Arthroscopic right rotator cuff repair","Findings: 2.5cm full-thickness supraspinatus tear","Double-row repair with suture anchors. EBL: minimal."]),
    ("TRANSURETHRAL RESECTION OF PROSTATE","SPINAL","UROLOGY","BENIGN PROSTATIC HYPERPLASIA",
     ["Procedure: TURP","Findings: Markedly enlarged prostate, 80 grams","EBL: 100ml. Foley catheter placed."]),
    ("LAPAROSCOPIC APPENDECTOMY","GENERAL","GENERAL SURGERY","ACUTE APPENDICITIS",
     ["Procedure: Laparoscopic appendectomy","Findings: Inflamed appendix without perforation","EBL: 15ml. No complications."]),
    ("PERMANENT PACEMAKER INSERTION","LOCAL WITH SEDATION","CARDIAC SURGERY","COMPLETE HEART BLOCK",
     ["Procedure: Dual-chamber permanent pacemaker implantation","Device: Medtronic Azure XT DR MRI","Lead positions: RA appendage and RV apex. Thresholds excellent."]),
    ("INGUINAL HERNIA REPAIR","GENERAL","GENERAL SURGERY","RIGHT INGUINAL HERNIA",
     ["Procedure: Laparoscopic right inguinal hernia repair with mesh","Findings: Direct inguinal hernia","Mesh: Bard 3DMax. EBL: minimal."]),
    ("CAROTID ENDARTERECTOMY","GENERAL","VASCULAR SURGERY","CAROTID STENOSIS 80%",
     ["Procedure: Right carotid endarterectomy","Findings: 80% stenosis right ICA","Patch angioplasty performed. EBL: 150ml."]),
    ("AORTIC VALVE REPLACEMENT","GENERAL","CARDIAC SURGERY","SEVERE AORTIC STENOSIS",
     ["Procedure: Aortic valve replacement with bioprosthetic valve","CPB time: 78 minutes. Cross-clamp: 55 minutes.","Edwards Inspiris Resilia 23mm valve placed. EBL: 500ml."]),
]

RAD_PROCEDURES = [
    ("CHEST X-RAY PA AND LATERAL","GENERAL RADIOLOGY",
     ["CHEST X-RAY PA AND LATERAL","FINDINGS: Heart size upper limits of normal.","Lungs clear bilaterally. No infiltrates or effusions.","IMPRESSION: No acute cardiopulmonary disease."]),
    ("CHEST X-RAY PA","GENERAL RADIOLOGY",
     ["CHEST X-RAY PA","FINDINGS: Heart size normal. Lungs clear.","IMPRESSION: No acute disease."]),
    ("CT HEAD WITHOUT CONTRAST","CT SCAN",
     ["CT HEAD WITHOUT CONTRAST","FINDINGS: No acute intracranial hemorrhage.","No mass effect or midline shift.","IMPRESSION: No acute intracranial abnormality."]),
    ("CT CHEST WITH CONTRAST","CT SCAN",
     ["CT CHEST WITH CONTRAST","FINDINGS: No pulmonary embolism.","No pulmonary nodules or masses.","IMPRESSION: No acute findings."]),
    ("CT ABDOMEN AND PELVIS WITH CONTRAST","CT SCAN",
     ["CT ABDOMEN AND PELVIS WITH CONTRAST","FINDINGS: No lymphadenopathy. Kidneys unremarkable.","No free fluid or mass.","IMPRESSION: No acute intra-abdominal pathology."]),
    ("MRI LUMBAR SPINE WITHOUT CONTRAST","MRI",
     ["MRI LUMBAR SPINE WITHOUT CONTRAST","FINDINGS: Disc degeneration at L4-5 and L5-S1.","Mild to moderate central canal stenosis.","IMPRESSION: Multilevel degenerative disc disease."]),
    ("MRI CERVICAL SPINE WITHOUT CONTRAST","MRI",
     ["MRI CERVICAL SPINE WITHOUT CONTRAST","FINDINGS: Disc bulges at C5-6 and C6-7.","Mild foraminal narrowing bilaterally.","IMPRESSION: Cervical spondylosis."]),
    ("MRI RIGHT KNEE WITHOUT CONTRAST","MRI",
     ["MRI RIGHT KNEE WITHOUT CONTRAST","FINDINGS: Grade III signal in medial meniscus.","Moderate joint effusion.","IMPRESSION: Medial meniscus tear with OA changes."]),
    ("ECHOCARDIOGRAM","ULTRASOUND",
     ["TRANSTHORACIC ECHOCARDIOGRAM","FINDINGS: LVEF estimated at 55%. Normal wall motion.","No significant valvular disease.","IMPRESSION: Normal cardiac function."]),
    ("ULTRASOUND ABDOMEN","ULTRASOUND",
     ["ULTRASOUND ABDOMEN COMPLETE","FINDINGS: Liver normal in size and echotexture.","No gallstones. Kidneys unremarkable.","IMPRESSION: Normal abdominal ultrasound."]),
    ("X-RAY LUMBAR SPINE","GENERAL RADIOLOGY",
     ["X-RAY LUMBAR SPINE AP AND LATERAL","FINDINGS: Degenerative disc disease L4-5 and L5-S1.","No fracture or listhesis.","IMPRESSION: Degenerative changes."]),
    ("DEXA SCAN","NUCLEAR MEDICINE",
     ["DEXA BONE DENSITY SCAN","FINDINGS: Lumbar spine T-score -1.8. Femoral neck T-score -1.5.","IMPRESSION: Osteopenia."]),
    ("MAMMOGRAM BILATERAL","GENERAL RADIOLOGY",
     ["BILATERAL MAMMOGRAM","FINDINGS: No suspicious calcifications or masses.","IMPRESSION: BI-RADS 1 - Negative."]),
    ("X-RAY BOTH KNEES","GENERAL RADIOLOGY",
     ["X-RAY BOTH KNEES AP AND LATERAL","FINDINGS: Bilateral medial compartment joint space narrowing.","Osteophyte formation. No fracture.","IMPRESSION: Moderate bilateral knee osteoarthritis."]),
]

# Note templates
NOTE_TEMPLATES = [
    ("PRIMARY CARE VISIT", [
        "Patient presents for routine follow-up.",
        "Vitals reviewed. Medications reconciled.",
        "Continue current regimen. Follow up in 3 months."]),
    ("PRIMARY CARE ANNUAL", [
        "Annual comprehensive examination.",
        "All screening tests up to date.",
        "Continue preventive care. Follow up in 1 year."]),
    ("MENTAL HEALTH FOLLOW-UP", [
        "Patient reports stable symptoms on current medications.",
        "PHQ-9 score reviewed. Sleep and appetite assessed.",
        "Continue current treatment plan."]),
    ("CARDIOLOGY FOLLOW-UP", [
        "Cardiac risk factors reviewed.",
        "Current medications optimized for cardiac protection.",
        "Echocardiogram and labs reviewed."]),
    ("ENDOCRINOLOGY FOLLOW-UP", [
        "Diabetes management reviewed. A1C discussed.",
        "Medication adjustments as indicated.",
        "Continue monitoring. Follow up in 3 months."]),
    ("PULMONOLOGY FOLLOW-UP", [
        "Pulmonary function reviewed. Inhaler technique assessed.",
        "Oxygen saturation noted.",
        "Continue current bronchodilator regimen."]),
    ("NEUROLOGY FOLLOW-UP", [
        "Neurological examination performed.",
        "Current symptoms and medications reviewed.",
        "Continue current management."]),
    ("RHEUMATOLOGY FOLLOW-UP", [
        "Joint examination performed. Inflammatory markers reviewed.",
        "Disease activity assessed.",
        "Continue DMARD therapy. Follow up in 3 months."]),
]

ADT_WARDS = ["ICU","TELEMETRY","SURGERY","NEUROLOGY","MEDICINE","STEP-DOWN"]
ADT_ROOMS = {
    "ICU": ["ICU-1A","ICU-2A","ICU-3A","ICU-4A","ICU-5B","ICU-6B"],
    "TELEMETRY": ["T101A","T102B","T205B","T301A","T108A","T210A"],
    "SURGERY": ["S101A","S102B","S204A","S303B","S105A","S206B"],
    "NEUROLOGY": ["N201A","N202B","N301A","N302B"],
    "MEDICINE": ["M101A","M102B","M201A","M202B","M301A","M302B"],
    "STEP-DOWN": ["SD101","SD102","SD201","SD202"],
}


# ── Helper functions ────────────────────────────────────────────────────────

def fm_date(year, month, day):
    """FileMan date: (year-1700)*10000 + month*100 + day"""
    return (year - 1700) * 10000 + month * 100 + day

def fm_datetime(year, month, day, hour, minute):
    frac = hour / 100 + minute / 10000
    return f"{fm_date(year, month, day)}.{hour:02d}{minute:02d}"

def random_fm_dob(min_age=25, max_age=90):
    age = random.randint(min_age, max_age)
    year = 2026 - age
    month = random.randint(1, 12)
    day = random.randint(1, 28)
    return fm_date(year, month, day)

def random_fm_service_dates():
    entry_year = random.randint(1960, 2015)
    sep_year = entry_year + random.randint(2, 8)
    em = random.randint(1, 12)
    ed = random.randint(1, 28)
    sm = random.randint(1, 12)
    sd = random.randint(1, 28)
    return fm_date(entry_year, em, ed), fm_date(sep_year, sm, sd)

def random_fm_clinical_date():
    year = random.choice([2024, 2025])
    month = random.randint(1, 12)
    day = random.randint(1, 28)
    return fm_date(year, month, day)

def random_fm_onset():
    year = random.randint(2008, 2025)
    month = random.randint(1, 12)
    day = random.randint(1, 28)
    return fm_date(year, month, day)

def random_fm_visit():
    year = 2025
    month = random.randint(6, 9)
    day = random.randint(1, 28)
    hour = random.randint(7, 15)
    minute = random.choice([0, 15, 30, 45])
    return fm_date(year, month, day), f"{hour:02d}{minute:02d}"

def abnormal_flag(val, ref_low, ref_high):
    try:
        v = float(val)
        lo = float(ref_low)
        hi = float(ref_high)
        if v > hi: return "H"
        if v < lo: return "L"
    except:
        pass
    return ""


# ── Generate patients ───────────────────────────────────────────────────────

print("Generating 500 patients...")

patients = []
used_names = set()

for ien in range(1, 501):
    # Alternate sex roughly 50/50
    sex = "M" if ien % 2 == 1 else "F"

    # Pick unique name
    while True:
        last = random.choice(LAST_NAMES)
        first = random.choice(MALE_FIRST if sex == "M" else FEMALE_FIRST)
        name = f"{last},{first}"
        if name not in used_names:
            used_names.add(name)
            break

    dob = random_fm_dob()
    ssn = f"9001{ien:05d}P"

    city, state, zipcode = random.choice(CITIES_STATES_ZIPS)
    area = random.choice(AREA_CODES)
    street_num = random.randint(100, 999)
    street_name = random.choice(STREETS)
    street_type = random.choice(STREET_TYPES)

    phone_base = f"{area}-555-{ien:04d}"
    work_phone = f"{area}-555-{(ien + 5000):04d}" if ien + 5000 <= 9999 else f"{area}-555-{ien % 10000:04d}"

    # Emergency contact
    emerg_rel = random.choice(EMERG_RELS)
    emerg_first = random.choice(MALE_FIRST if emerg_rel in ["SPOUSE","BROTHER","FATHER","SON","GRANDSON","FRIEND"] and sex == "F"
                                else FEMALE_FIRST if emerg_rel in ["SPOUSE","SISTER","MOTHER","DAUGHTER","GRANDDAUGHTER","FRIEND"] and sex == "M"
                                else MALE_FIRST)
    emerg_name = f"{last},{emerg_first}"
    emerg_phone = f"{area}-555-{(ien + 3000) % 10000:04d}"

    # Service
    branch = random.choice(BRANCHES)
    entry_fm, sep_fm = random_fm_service_dates()
    discharge = random.choices(["HONORABLE","GENERAL","OTHER THAN HONORABLE"], weights=[85,10,5])[0]
    pow_flag = "Y" if random.random() < 0.03 else "N"

    # SC%
    sc_pct = random.choice([0, 10, 20, 30, 40, 50, 60, 70, 80, 90, 100])
    if sc_pct == 0:
        elig = "NSC VETERAN"
        prim_elig = "NSC"
        vet = "Y"
    elif sc_pct < 50:
        elig = "SC VETERAN"
        prim_elig = "SC LESS THAN 50%"
        vet = "Y"
    else:
        elig = "SC VETERAN"
        prim_elig = "SC 50% TO 100%"
        vet = "Y"

    # Pick 1-4 problems for this patient
    num_problems = random.choices([1, 2, 3, 4], weights=[20, 35, 30, 15])[0]
    patient_problems = random.sample(PROBLEM_POOL, min(num_problems, len(PROBLEM_POOL)))

    # Pick 1-3 meds
    num_meds = random.choices([1, 2, 3, 4], weights=[25, 35, 25, 15])[0]
    patient_meds = random.sample(MED_POOL, min(num_meds, len(MED_POOL)))

    patients.append({
        "ien": ien, "name": name, "sex": sex, "dob": dob, "ssn": ssn,
        "street": f"{street_num} {street_name} {street_type}",
        "city": city, "state": state, "zip": zipcode,
        "phone": phone_base, "work_phone": work_phone,
        "emerg_name": emerg_name, "emerg_rel": emerg_rel, "emerg_phone": emerg_phone,
        "vet": vet, "sc_pct": sc_pct, "elig": elig, "prim_elig": prim_elig,
        "entry_fm": entry_fm, "sep_fm": sep_fm, "branch": branch, "discharge": discharge, "pow": pow_flag,
        "problems": patient_problems, "meds": patient_meds,
        "provider_dfn": 100 + (ien % 80) + 1,
    })


# ── Write patients.zwr ─────────────────────────────────────────────────────

print("Writing patients.zwr...")
with open(os.path.join(OUT_DIR, "patients.zwr"), "w", newline="\n") as f:
    f.write("; VistA PATIENT file #2 (^DPT) — 500 synthetic test patients\n")
    f.write("; Format: ^DPT(IEN,node)=\"piece1^piece2^...\"\n")
    f.write("; Node 0: Name^Sex^DOB(FM)^SSN\n")
    f.write("; Node .11: Street1^Street2^City^State^Zip\n")
    f.write("; Node .13: Phone (residence)\n")
    f.write("; Node .132: Phone (work)\n")
    f.write("; Node .33: EmergContactName^Relationship^Phone\n")
    f.write("; Node .36: Veteran^SC%^Eligibility^PrimaryEligibility\n")
    f.write("; Node .32: EntryDate(FM)^SepDate(FM)^Branch^DischargeType^POW\n")
    f.write(";\n")
    for p in patients:
        i = p["ien"]
        f.write(f'^DPT({i},0)="{p["name"]}^{p["sex"]}^{p["dob"]}^{p["ssn"]}"\n')
        f.write(f'^DPT({i},.11)="{p["street"]}^^{p["city"]}^{p["state"]}^{p["zip"]}"\n')
        f.write(f'^DPT({i},.13)="{p["phone"]}"\n')
        f.write(f'^DPT({i},.132)="{p["work_phone"]}"\n')
        f.write(f'^DPT({i},.33)="{p["emerg_name"]}^{p["emerg_rel"]}^{p["emerg_phone"]}"\n')
        f.write(f'^DPT({i},.36)="{p["vet"]}^{p["sc_pct"]}^{p["elig"]}^{p["prim_elig"]}"\n')
        f.write(f'^DPT({i},.32)="{p["entry_fm"]}^{p["sep_fm"]}^{p["branch"]}^{p["discharge"]}^{p["pow"]}"\n')


# ── Write allergies.zwr ────────────────────────────────────────────────────

print("Writing allergies.zwr...")
allergy_ien = 0
with open(os.path.join(OUT_DIR, "allergies.zwr"), "w", newline="\n") as f:
    f.write("; VistA PATIENT ALLERGIES file #120.8 (^GMR(120.8,...)) — 500-patient synthetic data\n")
    f.write('; Format: ^GMR(120.8,IEN,node)="piece1^piece2^..."\n')
    f.write("; Node 0: Allergen^AllergenType^Reactant^PatientDFN(;ptr)^^^ObservedHistorical\n")
    f.write("; Node 10,n,0: Reaction sign/symptom\n")
    f.write("; Node 14.5: Severity\n")
    f.write(";\n")
    for p in patients:
        num_allergies = random.choices([0, 1, 2, 3], weights=[15, 45, 30, 10])[0]
        if num_allergies == 0:
            continue
        all_allergens = ALLERGY_DRUGS + ALLERGY_CLASSES + ALLERGY_OTHER
        chosen = random.sample(all_allergens, min(num_allergies, len(all_allergens)))
        for allergen_name, allergen_type, reactant in chosen:
            allergy_ien += 1
            obs = random.choice(["OBSERVED", "HISTORICAL"])
            sev = random.choice(SEVERITIES)
            num_reactions = random.randint(1, 3)
            reactions = random.sample(ALLERGY_REACTIONS, num_reactions)

            if allergen_type in ("Drug", "Drug Class"):
                ref = reactant
            else:
                ref = reactant

            f.write(f'; --- Patient {p["ien"]} ({p["name"]}) ---\n')
            f.write(f'^GMR(120.8,{allergy_ien},0)="{allergen_name}^{allergen_type}^{ref}^{p["ien"]};DPT(^^^{obs}"\n')
            for ri, rxn in enumerate(reactions, 1):
                f.write(f'^GMR(120.8,{allergy_ien},10,{ri},0)="{rxn}"\n')
            f.write(f'^GMR(120.8,{allergy_ien},14.5)="{sev}"\n')


# ── Write problems.zwr ─────────────────────────────────────────────────────

print("Writing problems.zwr...")
prob_ien = 0
with open(os.path.join(OUT_DIR, "problems.zwr"), "w", newline="\n") as f:
    f.write("; VistA PROBLEM LIST file #9000011 (^AUPNPROB) — 500-patient synthetic data\n")
    f.write('; Format: ^AUPNPROB(IEN,node)="piece1^piece2^..."\n')
    f.write("; Node 0: Diagnosis^Condition^DateOnset(FM)^Status^PatientDFN(;ptr)\n")
    f.write("; Node 1: DiagnosisCode^Priority^ServiceConnected\n")
    f.write(";\n")
    for p in patients:
        for (cond, icd, chron, sc_prob) in p["problems"]:
            prob_ien += 1
            onset = random_fm_onset()
            status = "ACTIVE" if random.random() < 0.9 else "INACTIVE"
            sc_flag = "1" if random.random() < sc_prob and p["sc_pct"] > 0 else "0"
            f.write(f'^AUPNPROB({prob_ien},0)="{cond}^{chron}^{onset}^{status}^{p["ien"]};DPT("\n')
            f.write(f'^AUPNPROB({prob_ien},1)="{icd}^{chron}^{sc_flag}"\n')


# ── Write orders.zwr ───────────────────────────────────────────────────────

print("Writing orders.zwr...")
order_ien = 0
with open(os.path.join(OUT_DIR, "orders.zwr"), "w", newline="\n") as f:
    f.write("; VistA ORDER file #100 (^OR(100,...)) — 500-patient synthetic data\n")
    f.write('; Format: ^OR(100,IEN,node)="piece1^piece2^..."\n')
    f.write("; Node 0: Status^PatientDFN(;ptr)^ProviderDFN^StartDate(FM)^^Urgency\n")
    f.write("; Node 1: OrderType^OrderableItem\n")
    f.write(";\n")
    for p in patients:
        num_orders = random.choices([1, 2, 3], weights=[30, 45, 25])[0]
        for _ in range(num_orders):
            order_ien += 1
            status = random.choices(["ACTIVE","COMPLETE","DISCONTINUED"], weights=[60,30,10])[0]
            urgency = random.choices(["ROUTINE","STAT"], weights=[90,10])[0]
            start_date = random_fm_clinical_date()

            order_cat = random.choices(["PHARMACY","LAB","CONSULT"], weights=[60,25,15])[0]
            if order_cat == "PHARMACY":
                item = random.choice(ORDER_TYPES_PHARMACY)
            elif order_cat == "LAB":
                item = random.choice(ORDER_TYPES_LAB)
            else:
                item = random.choice(ORDER_TYPES_CONSULT)

            f.write(f'^OR(100,{order_ien},0)="{status}^{p["ien"]};DPT(^{p["provider_dfn"]}^{start_date}^^{urgency}"\n')
            f.write(f'^OR(100,{order_ien},1)="{order_cat}^{item}"\n')


# ── Write labs.zwr ──────────────────────────────────────────────────────────

print("Writing labs.zwr...")
with open(os.path.join(OUT_DIR, "labs.zwr"), "w", newline="\n") as f:
    f.write('; VistA LAB DATA file #63 (^LR(63,...)) — 500-patient synthetic Chemistry (CH) results\n')
    f.write('; Format: ^LR(63,PatientDFN,"CH",FMDate,Seq)="^^^TestName^Value^Units^RefLow^RefHigh^AbnFlag"\n')
    f.write("; Note: DFN matches patient IEN from ^DPT\n")
    f.write(";\n")
    for p in patients:
        num_labs = random.choices([2, 3, 4, 5], weights=[25, 35, 25, 15])[0]
        lab_date = random_fm_clinical_date()
        tests = random.sample(list(LAB_TESTS.keys()), num_labs)
        for seq, test_name in enumerate(tests, 1):
            units, ref_lo, ref_hi, gen_val = LAB_TESTS[test_name]
            val = gen_val()
            flag = abnormal_flag(val, ref_lo, ref_hi)
            f.write(f'^LR(63,{p["ien"]},"CH",{lab_date},{seq})="^^^{test_name}^{val}^{units}^{ref_lo}^{ref_hi}^{flag}"\n')


# ── Write vitals.zwr ───────────────────────────────────────────────────────

print("Writing vitals.zwr...")
vital_ien = 0
with open(os.path.join(OUT_DIR, "vitals.zwr"), "w", newline="\n") as f:
    f.write("; VistA GMRV VITAL MEASUREMENT file #120.5 (^GMR(120.5,...)) — 500-patient synthetic data\n")
    f.write('; Format: ^GMR(120.5,IEN,0)="DateTimeTaken(FM)^VitalType^Value^PatientDFN(;ptr)"\n')
    f.write("; Optional qualifier sub-nodes: ^GMR(120.5,IEN,5,n,0)=\"Qualifier\"\n")
    f.write(";\n")
    for p in patients:
        date_fm, time_str = random_fm_visit()
        dt = f"{date_fm}.{time_str}"

        # Everyone gets BP and pulse
        sys = random.randint(100, 175)
        dia = random.randint(60, 105)
        vital_ien += 1
        f.write(f'^GMR(120.5,{vital_ien},0)="{dt}^BLOOD PRESSURE^{sys}/{dia}^{p["ien"]};DPT("\n')

        pulse = random.randint(55, 100)
        vital_ien += 1
        f.write(f'^GMR(120.5,{vital_ien},0)="{dt}^PULSE^{pulse}^{p["ien"]};DPT("\n')

        # Weight
        weight = random.randint(100, 250)
        vital_ien += 1
        f.write(f'^GMR(120.5,{vital_ien},0)="{dt}^WEIGHT^{weight}^{p["ien"]};DPT("\n')

        # Optional extras
        if random.random() < 0.4:
            temp = round(random.uniform(97.0, 100.5), 1)
            vital_ien += 1
            f.write(f'^GMR(120.5,{vital_ien},0)="{dt}^TEMPERATURE^{temp}^{p["ien"]};DPT("\n')

        if random.random() < 0.3:
            resp = random.randint(12, 24)
            vital_ien += 1
            f.write(f'^GMR(120.5,{vital_ien},0)="{dt}^RESPIRATION^{resp}^{p["ien"]};DPT("\n')

        if random.random() < 0.25:
            spo2 = random.randint(88, 100)
            vital_ien += 1
            f.write(f'^GMR(120.5,{vital_ien},0)="{dt}^PULSE OXIMETRY^{spo2}^{p["ien"]};DPT("\n')

        if random.random() < 0.2:
            pain = random.randint(1, 10)
            loc = random.choice(PAIN_LOCATIONS)
            vital_ien += 1
            f.write(f'^GMR(120.5,{vital_ien},0)="{dt}^PAIN^{pain}^{p["ien"]};DPT("\n')
            f.write(f'^GMR(120.5,{vital_ien},5,1,0)="{loc}"\n')


# ── Write tiu.zwr ──────────────────────────────────────────────────────────

print("Writing tiu.zwr...")
tiu_ien = 0
with open(os.path.join(OUT_DIR, "tiu.zwr"), "w", newline="\n") as f:
    f.write("; VistA TIU DOCUMENT file #8925 (^TIU(8925,...)) — 500-patient synthetic data\n")
    f.write('; Format: ^TIU(8925,IEN,0)="DocumentType^PatientDFN(;ptr)^AuthorDFN^^^^ReferenceDate(FM)"\n')
    f.write('; Text: ^TIU(8925,IEN,"TEXT",line,0)="line of text"\n')
    f.write(";\n")
    for p in patients:
        # ~80% of patients get a note
        if random.random() < 0.2:
            continue
        tiu_ien += 1
        ref_date = random_fm_clinical_date()
        doc_type = random.choices(["PROGRESS NOTE","TELEPHONE NOTE","DISCHARGE SUMMARY"],
                                   weights=[80,10,10])[0]
        template_title, template_lines = random.choice(NOTE_TEMPLATES)

        f.write(f'^TIU(8925,{tiu_ien},0)="{doc_type}^{p["ien"]};DPT(^{p["provider_dfn"]}^^^^{ref_date}"\n')
        f.write(f'^TIU(8925,{tiu_ien},"TEXT",1,0)="{template_title}"\n')
        for li, line in enumerate(template_lines, 2):
            f.write(f'^TIU(8925,{tiu_ien},"TEXT",{li},0)="{line}"\n')


# ── Write consults.zwr ─────────────────────────────────────────────────────

print("Writing consults.zwr...")
consult_ien = 0
with open(os.path.join(OUT_DIR, "consults.zwr"), "w", newline="\n") as f:
    f.write("; VistA REQUEST/CONSULTATION file #123 (^GMR(123,...)) — 500-patient synthetic data\n")
    f.write('; Format: ^GMR(123,IEN,0)="ToService^PatientDFN(;ptr)^Urgency^FromService^RequestingProvider"\n')
    f.write('; Reason: ^GMR(123,IEN,20,n,0)="reason text"\n')
    f.write(";\n")
    for p in patients:
        # ~40% of patients get a consult
        if random.random() < 0.6:
            continue
        num_consults = random.choices([1, 2], weights=[70, 30])[0]
        services_chosen = random.sample(CONSULT_SERVICES, min(num_consults, len(CONSULT_SERVICES)))
        for svc in services_chosen:
            consult_ien += 1
            urgency = random.choices(["ROUTINE","STAT"], weights=[85,15])[0]
            from_svc = random.choice(["PRIMARY CARE","EMERGENCY","MENTAL HEALTH","SPINE CLINIC"])
            provider = random.choice(DOCTOR_NAMES)
            reasons = random.choice(CONSULT_REASONS.get(svc, [["Evaluate and treat."]]))
            if isinstance(reasons, str):
                reasons = [reasons]

            f.write(f'^GMR(123,{consult_ien},0)="{svc}^{p["ien"]};DPT(^{urgency}^{from_svc}^{provider}"\n')
            for ri, reason in enumerate(reasons if isinstance(reasons, list) else [reasons], 1):
                f.write(f'^GMR(123,{consult_ien},20,{ri},0)="{reason}"\n')


# ── Write surgery.zwr ──────────────────────────────────────────────────────

print("Writing surgery.zwr...")
surg_ien = 0
surgery_patients = []  # Track for ADT
with open(os.path.join(OUT_DIR, "surgery.zwr"), "w", newline="\n") as f:
    f.write("; VistA SURGERY file #130 (^SRF) — 500-patient synthetic data\n")
    f.write('; Format: ^SRF(IEN,0)="PatientDFN(;ptr)^Procedure^DateOfOp(FM)^SurgeonDFN^Anesthesia^Specialty^PreOpDiag"\n')
    f.write('; Operative report: ^SRF(IEN,"OP",n,0)="text"\n')
    f.write(";\n")
    for p in patients:
        # ~12% of patients get surgery
        if random.random() < 0.88:
            continue
        proc = random.choice(SURGERY_PROCEDURES)
        proc_name, anesthesia, specialty, preop_diag, op_lines = proc
        surg_ien += 1
        surgeon_dfn = 200 + (surg_ien % 20) + 1
        op_date = random_fm_clinical_date()

        f.write(f'^SRF({surg_ien},0)="{p["ien"]};DPT(^{proc_name}^{op_date}^{surgeon_dfn}^{anesthesia}^{specialty}^{preop_diag}"\n')
        f.write(f'^SRF({surg_ien},"OP",1,0)="OPERATIVE REPORT"\n')
        for li, line in enumerate(op_lines, 2):
            f.write(f'^SRF({surg_ien},"OP",{li},0)="{line}"\n')

        surgery_patients.append((p, op_date, specialty))


# ── Write radiology.zwr ────────────────────────────────────────────────────

print("Writing radiology.zwr...")
rad_ien = 0
with open(os.path.join(OUT_DIR, "radiology.zwr"), "w", newline="\n") as f:
    f.write("; VistA RAD/NUC MED ORDERS file #75.1 (^RA(75.1,...)) — 500-patient synthetic data\n")
    f.write('; Format: ^RA(75.1,IEN,0)="PatientDFN(;ptr)^Procedure^ImagingType^Urgency^RequestingProvider^ClinicalHistory"\n')
    f.write('; Report: ^RA(75.1,IEN,"RPT",n,0)="text"\n')
    f.write(";\n")
    for p in patients:
        # ~30% of patients get imaging
        if random.random() < 0.7:
            continue
        num_studies = random.choices([1, 2], weights=[70, 30])[0]
        chosen_rads = random.sample(RAD_PROCEDURES, min(num_studies, len(RAD_PROCEDURES)))
        for proc_name, imaging_type, rpt_lines in chosen_rads:
            rad_ien += 1
            urgency = random.choices(["ROUTINE","STAT"], weights=[85,15])[0]
            provider = random.choice(DOCTOR_NAMES)
            history = random.choice([
                "Follow-up evaluation","Annual screening","New symptoms",
                "Rule out acute process","Post-operative evaluation",
                "Baseline evaluation","Chronic condition monitoring",
            ])

            f.write(f'^RA(75.1,{rad_ien},0)="{p["ien"]};DPT(^{proc_name}^{imaging_type}^{urgency}^{provider}^{history}"\n')
            for li, line in enumerate(rpt_lines, 1):
                f.write(f'^RA(75.1,{rad_ien},"RPT",{li},0)="{line}"\n')


# ── Write adt.zwr ──────────────────────────────────────────────────────────

print("Writing adt.zwr...")
adt_ien = 0
with open(os.path.join(OUT_DIR, "adt.zwr"), "w", newline="\n") as f:
    f.write("; VistA PATIENT MOVEMENT file #405 (^DGPT) — 500-patient synthetic data\n")
    f.write('; Format: ^DGPT(IEN,0)="PatientDFN(;ptr)^TransactionType^MovementDT(FM)^Ward^RoomBed^TreatingSpec^AttendPhys^Diagnosis"\n')
    f.write(";\n")
    for p_data, op_date, specialty in surgery_patients:
        physician = random.choice(DOCTOR_NAMES)
        admit_ward = random.choice(["ICU","SURGERY"])
        admit_room = random.choice(ADT_ROOMS[admit_ward])

        # Parse op_date to create admission/discharge
        admit_dt = f"{op_date}.0700"

        adt_ien += 1
        f.write(f'^DGPT({adt_ien},0)="{p_data["ien"]};DPT(^ADMISSION^{admit_dt}^{admit_ward}^{admit_room}^{specialty}^{physician}^POST-OPERATIVE CARE"\n')

        # Some patients get a transfer
        if admit_ward == "ICU" and random.random() < 0.7:
            transfer_ward = random.choice(["TELEMETRY","MEDICINE","STEP-DOWN"])
            transfer_room = random.choice(ADT_ROOMS[transfer_ward])
            adt_ien += 1
            f.write(f'^DGPT({adt_ien},0)="{p_data["ien"]};DPT(^TRANSFER^{op_date + 2}.1000^{transfer_ward}^{transfer_room}^{specialty}^{physician}^POST-OPERATIVE RECOVERY"\n')
            discharge_ward = transfer_ward
            discharge_room = transfer_room
            discharge_day_offset = random.randint(4, 8)
        else:
            discharge_ward = admit_ward
            discharge_room = admit_room
            discharge_day_offset = random.randint(1, 4)

        adt_ien += 1
        f.write(f'^DGPT({adt_ien},0)="{p_data["ien"]};DPT(^DISCHARGE^{op_date + discharge_day_offset}.1000^{discharge_ward}^{discharge_room}^{specialty}^{physician}^DISCHARGED STABLE"\n')


# ── Write pharmacy.zwr ─────────────────────────────────────────────────────

print("Writing pharmacy.zwr...")
rx_ien = 0
with open(os.path.join(OUT_DIR, "pharmacy.zwr"), "w", newline="\n") as f:
    f.write("; VistA PRESCRIPTION file #52 (^PS(52,...)) — 500-patient synthetic data\n")
    f.write('; Format: ^PS(52,IEN,0)="PatientDFN(;ptr)^Drug^Dosage^Route^Schedule^Sig^DaysSupply^Qty^Refills^ProviderDFN"\n')
    f.write(";\n")
    for p in patients:
        for med in p["meds"]:
            rx_ien += 1
            drug, dose, route, sched, sig, days, qty, refills = med
            f.write(f'^PS(52,{rx_ien},0)="{p["ien"]};DPT(^{drug}^{dose}^{route}^{sched}^{sig}^{days}^{qty}^{refills}^{p["provider_dfn"]}"\n')


# ── Summary ─────────────────────────────────────────────────────────────────

print(f"\n=== Generation Complete ===")
print(f"Patients:     500")
print(f"Allergies:    {allergy_ien} records")
print(f"Problems:     {prob_ien} records")
print(f"Orders:       {order_ien} records")
print(f"Vitals:       {vital_ien} measurements")
print(f"TIU Notes:    {tiu_ien} documents")
print(f"Consults:     {consult_ien} referrals")
print(f"Surgeries:    {surg_ien} cases")
print(f"Radiology:    {rad_ien} studies")
print(f"ADT:          {adt_ien} movements")
print(f"Prescriptions:{rx_ien} Rx records")
print(f"\nOutput: {OUT_DIR}")
