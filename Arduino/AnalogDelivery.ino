
int referenceTop = 160;
int referenceBottom = 80;
int delayAmount = 100;
int currentReference = referenceBottom;
int counter = 0;

typedef 
  union {
    struct {
    bool A0on     : 1;
    bool A1on     : 1;
    bool A2on     : 1;
    bool A3on     : 1;
    bool A4on     : 1;
    bool A5on     : 1;
    bool RESERVED : 1;
    bool refon    : 1;
    };
    byte raw; 
} configuration_t;

configuration_t options;

void setup() {
  
  options.raw = 0xff;
   pinMode(A0, INPUT);
   pinMode(A1, INPUT);
   pinMode(A2, INPUT);
   pinMode(A3, INPUT);
   pinMode(A4, INPUT);
   pinMode(A5, INPUT);
   pinMode(5, OUTPUT);
   pinMode(5, OUTPUT);
   pinMode(6, OUTPUT);  

   //Test signals (needs smoothing with an R/C filter)
   analogWrite(3, referenceBottom);
   analogWrite(5, 60);
   analogWrite(6, 200);

   Serial.begin(9600);
}

void loop() {
  if(Serial.available() > 0)
  {
     options.raw = Serial.read();
  }
  
  if(options.A0on)
  {
    int s0 = analogRead(A0);
    Serial.print("A0:");
    Serial.println(s0);
  }
  
  if(options.A1on)
  {
    int s1 = analogRead(A1);
    Serial.print("A1:");
    Serial.println(s1);
  }
  
  if(options.A2on)
  {
    int s2 = analogRead(A2);
    Serial.print("A2:");
    Serial.println(s2);
  }
  
  if(options.A3on)
  {
    int s3 = analogRead(A3);
    Serial.print("A3:");
    Serial.println(s3);
  }
  
  if(options.A4on)
  {
    int s4 = analogRead(A4);
    Serial.print("A4:");
    Serial.println(s4);
  }
  
  if(options.A5on)
  {
    int s5 = analogRead(A5);
    Serial.print("A5:");
    Serial.println(s5);
  }
  
  //reference signals
  if(options.refon)
  {
    if(++counter == delayAmount)
    {
      if(currentReference == referenceBottom)
      {
        currentReference = referenceTop;
        counter = 0;
      }
      else
      {
        currentReference = referenceBottom;
        counter = 0;
      }
      analogWrite(3, currentReference);
    }
  }
}
